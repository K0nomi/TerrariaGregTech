#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Api.Capability;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;

public sealed class EnergyNet
{
	// per-tile cable loss is divided by this factor, our cables are 1x1 tiles while machines are 2x2
	public const long TileLossDivisor = 2;

	public IReadOnlyDictionary<(int x, int y), CableCell> Cells { get; }
	public VoltageTier EffectiveTier { get; }
	public int EffectiveAmperage { get; }
	public int MaxLossPerAmp { get; }

	public List<(int x, int y, IEnergyContainer ep)> ProducerLinks { get; } = new();
	public List<(int x, int y, IEnergyContainer ep)> ConsumerLinks { get; } = new();
	public List<IEnergyContainer> Producers { get; } = new();
	public List<IEnergyContainer> Consumers { get; } = new();

	private readonly Dictionary<(int x, int y), List<EnergyRoutePath>> _routesByCable = new();
	private readonly Dictionary<(int x, int y), long> _cableAmpsThisTick = new();

	public long LastTickExtracted { get; private set; }
	public long LastTickDelivered { get; private set; }
	public long LastTickWasted    => LastTickExtracted - LastTickDelivered;

	public (int x, int y) AnchorCell { get; }

	public EnergyNet(NetworkComponent component)
	{
		Cells = component.Cells;
		EffectiveTier = component.EffectiveTier;
		EffectiveAmperage = component.EffectiveAmperage;
		MaxLossPerAmp = component.MaxLossPerAmp;

		var anchor = (x: int.MaxValue, y: int.MaxValue);
		foreach (var k in Cells.Keys)
		{
			if (k.x < anchor.x || (k.x == anchor.x && k.y < anchor.y)) anchor = k;
		}
		AnchorCell = anchor;
	}

	public long PerTickCapacity => VoltageTiers.Voltage(EffectiveTier) * EffectiveAmperage;

	public float SmoothedLoad { get; private set; }

	public void AdvanceLoadSmoothing(float rate)
	{
		long cap = PerTickCapacity;
		float target = (cap > 0 && LastTickDelivered > 0)
			? System.Math.Min(1f, LastTickDelivered / (float)cap)
			: 0f;
		SmoothedLoad += (target - SmoothedLoad) * rate;
		if (SmoothedLoad < 0.0005f) SmoothedLoad = 0f;
	}

	public void SetSmoothedLoad(float v) => SmoothedLoad = v;   // client-applied from sync

	private System.Func<(int x, int y), IEnergyContainer?>? _endpointLookup;
	internal void SetEndpointLookup(System.Func<(int x, int y), IEnergyContainer?> lookup) =>
		_endpointLookup = lookup;

	public void Tick()
	{
		LastTickExtracted = 0;
		LastTickDelivered = 0;
		_cableAmpsThisTick.Clear();

		foreach (var p in Producers)
		{
			long pushV = p.OutputVoltage;
			if (pushV <= 0) continue;
			var pushTier = VoltageTiers.MaxTierForVoltage(pushV);
			if ((int)pushTier > (int)EffectiveTier)
			{
				BurnUndertierCables(pushTier);
				return;
			}
		}

		foreach (var c in Consumers)
		{
			long inV = c.InputVoltage;
			if (inV <= 0) continue;
			var inputTier = VoltageTiers.MaxTierForVoltage(inV);
			if ((int)EffectiveTier > (int)inputTier)
				ExplodeConsumer(c);
		}

		if (ProducerLinks.Count == 0 || ConsumerLinks.Count == 0) return;

		var producersPushedThisTick = new HashSet<IEnergyContainer>();
		foreach (var (cx, cy, producer) in ProducerLinks)
		{
			if (producer.OutputVoltage <= 0) continue;
			if (producersPushedThisTick.Contains(producer)) continue;
			long voltage  = producer.OutputVoltage;
			long amperage = producer.GetPushAmperage();
			if (voltage <= 0 || amperage <= 0) continue;
			producersPushedThisTick.Add(producer);

			long ampsUsed = RoutePush((cx, cy), producer, voltage, amperage);
			if (ampsUsed > 0)
				producer.OnEnergyPushedToNetwork(ampsUsed, voltage);
			LastTickExtracted += ampsUsed * voltage;
		}
	}

	private long RoutePush((int x, int y) sourceCable, IEnergyContainer producer, long voltage, long amperage)
	{
		var routes = GetRoutes(sourceCable);
		long ampsUsed = 0;
		foreach (var path in routes)
		{
			long effectiveLoss = path.Loss / TileLossDivisor;
			if (effectiveLoss >= voltage) continue;
			if (ReferenceEquals(path.Target, producer)) continue;

			var dest = path.Target;
			var destFace = dest.EnergyFaceForCell(path.TargetCablePos.x, path.TargetCablePos.y);
			if (!dest.InputsEnergy(destFace)) continue;

			long pathVoltage = voltage - effectiveLoss;

			bool cableBroken = false;
			foreach (var cablePos in path.Cables)
			{
				var cell = Cells.TryGetValue(cablePos, out var c) ? (CableCell?)c : null;
				if (cell is null) { cableBroken = true; break; }
				long cableMaxV = VoltageTiers.Voltage(cell.Value.Voltage);
				if (cableMaxV < voltage)
				{
					BurnCable(cablePos);
					cableBroken = true;
					break;
				}
				pathVoltage = System.Math.Min(cableMaxV, pathVoltage);
			}
			if (cableBroken) continue;

			long amps = dest.AcceptEnergyFromNetwork(destFace, pathVoltage, amperage - ampsUsed);
			if (amps == 0) continue;

			ampsUsed += amps;

			foreach (var cablePos in path.Cables)
			{
				_cableAmpsThisTick.TryGetValue(cablePos, out long used);
				used += amps;
				_cableAmpsThisTick[cablePos] = used;
				if (Cells.TryGetValue(cablePos, out var cc) && used > cc.TotalAmperage)
				{
					BurnCable(cablePos);
				}
			}

			LastTickDelivered += amps * pathVoltage;
			if (ampsUsed >= amperage) break;
		}
		return ampsUsed;
	}

	private List<EnergyRoutePath> GetRoutes((int x, int y) sourceCable)
	{
		if (_routesByCable.TryGetValue(sourceCable, out var cached)) return cached;
		var lookup = _endpointLookup;
		if (lookup is null) return new List<EnergyRoutePath>();
		EnergyNetWalker.TryGetEndpoint del = ((int x, int y) pos, out IEnergyContainer ep) =>
		{
			var found = lookup(pos);
			if (found is null) { ep = null!; return false; }
			ep = found;
			return true;
		};
		var routes = EnergyNetWalker.CreateNetData(CableLayerSystem.Cables, sourceCable, del);
		_routesByCable[sourceCable] = routes;
		return routes;
	}

	private void BurnCable((int x, int y) pos)
	{
		var cell = CableLayerSystem.Cables.CellAt(pos.x, pos.y);

		TerrariaCompat.Net.BlockExplosionEffectPacket.PlayLocal(pos.x, pos.y, 1, 1);
		if (Main.netMode == NetmodeID.Server)
			TerrariaCompat.Net.BlockExplosionEffectPacket.Send(pos.x, pos.y, 1, 1);

		if (cell is { } c && Main.netMode != NetmodeID.MultiplayerClient)
		{
			int? itemType = WireItemRegistry.Get(c.MaterialId, c.WireSize, c.Insulated);
			if (itemType is not null)
			{
				int worldX = pos.x * 16;
				int worldY = pos.y * 16;
				Item.NewItem(new EntitySource_TileBreak(pos.x, pos.y),
					worldX, worldY, 16, 16, itemType.Value);
			}
		}

		CableLayerSystem.Cables.Remove(pos.x, pos.y);
		TerrariaCompat.Net.CablePackets.SendRemoveBroadcast(pos.x, pos.y);
		_routesByCable.Clear();
	}

	private void BurnUndertierCables(VoltageTier producerTier)
	{
		var toRemove = new List<(int x, int y)>();
		foreach (var kv in Cells)
			if ((int)kv.Value.Voltage < (int)producerTier)
				toRemove.Add(kv.Key);
		foreach (var pos in toRemove) BurnCable(pos);
	}

	private const float LossDangerFraction = 0.5f;
	private Dictionary<(int x, int y), long>? _lossFromSource;
	private long _maxProducerVoltage;

	public bool IsHighLossCable(int x, int y) => GetCableLossPercent(x, y) >= LossDangerFraction;

	public float GetCableLossPercent(int x, int y)
	{
		EnsureLossMap();
		if (_maxProducerVoltage <= 0) return 0f;
		if (_lossFromSource is null) return 0f;
		if (!_lossFromSource.TryGetValue((x, y), out long loss)) return 0f;
		return System.Math.Min(1f, (float)loss / TileLossDivisor / _maxProducerVoltage);
	}

	private void EnsureLossMap()
	{
		if (_lossFromSource is not null) return;
		_lossFromSource = new Dictionary<(int x, int y), long>();
		_maxProducerVoltage = 0;
		foreach (var p in Producers)
			if (p.OutputVoltage > _maxProducerVoltage) _maxProducerVoltage = p.OutputVoltage;
		if (ProducerLinks.Count == 0 || Cells.Count == 0) return;

		var pq = new SortedSet<(long loss, int x, int y)>();
		foreach (var (cx, cy, _) in ProducerLinks)
		{
			if (!Cells.TryGetValue((cx, cy), out var cell)) continue;
			long initial = cell.LossPerAmp;
			if (!_lossFromSource.TryGetValue((cx, cy), out long existing) || initial < existing)
			{
				_lossFromSource[(cx, cy)] = initial;
				pq.Add((initial, cx, cy));
			}
		}

		while (pq.Count > 0)
		{
			var (loss, x, y) = pq.Min;
			pq.Remove(pq.Min);
			if (_lossFromSource.TryGetValue((x, y), out long best) && loss > best) continue;
			foreach (var (dx, dy) in s_dirs)
			{
				int nx = x + dx, ny = y + dy;
				if (!Cells.TryGetValue((nx, ny), out var ncell)) continue;
				long newLoss = loss + ncell.LossPerAmp;
				if (!_lossFromSource.TryGetValue((nx, ny), out long curr) || newLoss < curr)
				{
					_lossFromSource[(nx, ny)] = newLoss;
					pq.Add((newLoss, nx, ny));
				}
			}
		}
	}

	private static readonly (int dx, int dy)[] s_dirs =
		{ (0, -1), (0, 1), (-1, 0), (1, 0) };

	private static void ExplodeConsumer(IEnergyContainer consumer)
	{
		if (consumer is not TerrariaCompat.Machine.MetaMachine machine) return;
		Terraria.ModLoader.ModContent.GetInstance<GregTechCEuTerraria>()
			?.Logger?.Warn(
				$"[wire-net overtier] consumer {machine.GetType().Name} at " +
				$"({machine.Position.X},{machine.Position.Y}) " +
				$"InputVoltage={(consumer is IEnergyContainer ec1 ? ec1.InputVoltage : 0)} V " +
				$"- EXPLODING");
		Common.Machine.Trait.EnvironmentalExplosionTrait.DoExplosionAt(machine,
			Common.Machine.Trait.EnvironmentalExplosionTrait.GetExplosionPower(
				machine is IEnergyContainer ec ? ec.InputVoltage : 0));
	}
}
