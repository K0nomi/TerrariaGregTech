#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;

namespace GregTechCEuTerraria.Api.Pipenet;

// port of com.gregtechceu.gtceu.api.pipenet.PipeNetWalker
// adaptations:
//  cells are struct values in a flat layer dict
//  any cell at a cardinal neighbor connects
//  no per-world isolation since EnergyNetSystem is global.
public abstract class PipeNetWalker<TCell, TNodeData, TNet>
	where TCell : struct
	where TNet : class
{
	public TNet PipeNet { get; }

	public (int x, int y) CurrentPos { get; protected set; }

	public int WalkedBlocks { get; protected set; }

	public bool Invalid { get; private set; }

	public bool Failed { get; private set; }

	public bool Running => Root._running;
	private bool _running;

	protected PipeNetWalker<TCell, TNodeData, TNet> Root;
	private HashSet<(int x, int y)>? _walked;
	protected List<PipeNetWalker<TCell, TNodeData, TNet>>? Walkers;
	protected readonly List<IODirection> NextPipeFacings = new(5);
	protected readonly List<TCell> NextPipes = new(5);
	protected readonly List<(int x, int y)> NextPipePositions = new(5);
	protected TCell? CurrentPipe;
	private IODirection? _from;

	protected PipeNetWalker(TNet pipeNet, (int x, int y) sourcePipe, int walkedBlocks)
	{
		PipeNet = pipeNet;
		WalkedBlocks = walkedBlocks;
		CurrentPos = sourcePipe;
		Root = this;
	}

	protected abstract PipeNetWalker<TCell, TNodeData, TNet> CreateSubWalker(
		TNet pipeNet, IODirection facingToNextPos, (int x, int y) nextPos, int walkedBlocks);

	protected abstract void CheckPipe(TCell pipeTile, (int x, int y) pos);

	protected virtual void CheckSelfPos(TCell pipeTile, (int x, int y) pos) { }

	protected abstract bool TryGetCellAt((int x, int y) pos, out TCell cell);

	protected virtual void CheckNeighbour(
		TCell pipeNode, (int x, int y) pipePos, IODirection faceToNeighbour, object? neighbourTile) { }

	protected virtual bool IsValidPipe(
		TCell currentPipe, TCell neighbourPipe, (int x, int y) pipePos, IODirection faceToNeighbour) => true;

	protected virtual IReadOnlyList<(IODirection side, int dx, int dy)> GetSurroundingPipeSides() =>
		IODirectionExtensions.Cardinal4;

	// Pipe Intersection enabled
	protected virtual bool SupportsCrossover => false;

	protected virtual void OnRemoveSubWalker(PipeNetWalker<TCell, TNodeData, TNet> subWalker) { }

	protected virtual object? ResolveNeighborTile((int x, int y) pos) => null;

	public void TraversePipeNet() => TraversePipeNet(32768);

	public void TraversePipeNet(int maxWalks)
	{
		if (Invalid)
			throw new System.InvalidOperationException(
				"This walker already walked. Create a new one if you want to walk again");
		Root = this;
		_walked = new HashSet<(int, int)>();
		int i = 0;
		_running = true;
		while (_running && !Walk() && i++ < maxWalks) { }
		_running = false;
		Root._walked?.Clear();
		Invalid = true;
	}

	public void Stop() => Root._running = false;

	private bool Walk()
	{
		if (Walkers == null)
		{
			if (!CheckPos())
			{
				Root.Failed = true;
				return true;
			}

			if (NextPipeFacings.Count == 0)
				return true;
			if (NextPipeFacings.Count == 1)
			{
				CurrentPos = NextPipePositions[0];
				CurrentPipe = NextPipes[0];
				_from = NextPipeFacings[0].Opposite();
				WalkedBlocks++;
				return !Running;
			}

			Walkers = new List<PipeNetWalker<TCell, TNodeData, TNet>>();
			for (int i = 0; i < NextPipeFacings.Count; i++)
			{
				var side = NextPipeFacings[i];
				var walker = CreateSubWalker(PipeNet, side, NextPipePositions[i], WalkedBlocks + 1)
					?? throw new System.InvalidOperationException("Walker can't be null");
				walker.Root = Root;
				walker.CurrentPipe = NextPipes[i];
				walker._from = side.Opposite();
				Walkers.Add(walker);
			}
		}

		for (int i = Walkers.Count - 1; i >= 0; i--)
		{
			var walker = Walkers[i];
			if (walker.Walk())
			{
				OnRemoveSubWalker(walker);
				Walkers.RemoveAt(i);
			}
		}

		return !Running || Walkers.Count == 0;
	}

	private bool CheckPos()
	{
		NextPipeFacings.Clear();
		NextPipes.Clear();
		NextPipePositions.Clear();
		if (CurrentPipe == null)
		{
			if (!TryGetCellAt(CurrentPos, out var cell))
				return false;
			CurrentPipe = cell;
		}
		var pipeTile = CurrentPipe.Value;
		CheckPipe(pipeTile, CurrentPos);
		CheckSelfPos(pipeTile, CurrentPos);
		Root._walked!.Add(CurrentPos);

		foreach (var (accessSide, dx, dy) in GetSurroundingPipeSides())
		{
			if (accessSide == _from) continue;

			var immediate = (CurrentPos.x + dx, CurrentPos.y + dy);
			object? neighborTile = ResolveNeighborTile(immediate);
			var pipePos = SupportsCrossover
				? PipePassthrough.EffectiveNeighbor(CurrentPos.x, CurrentPos.y, dx, dy)
				: immediate;
			if (TryGetCellAt(pipePos, out var otherPipe))
			{
				if (IsWalked(pipePos)) continue;
				if (IsValidPipe(pipeTile, otherPipe, CurrentPos, accessSide))
				{
					NextPipeFacings.Add(accessSide);
					NextPipes.Add(otherPipe);
					NextPipePositions.Add(pipePos);
					continue;
				}
			}
			CheckNeighbour(pipeTile, CurrentPos, accessSide, neighborTile);
		}
		return true;
	}

	protected bool IsWalked((int x, int y) pos) => Root._walked!.Contains(pos);
}
