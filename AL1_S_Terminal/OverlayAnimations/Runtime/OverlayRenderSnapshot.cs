namespace AL1_S_Terminal.OverlayAnimations.Runtime;



public sealed class OverlayRenderSnapshot

{

	public IReadOnlyList<OverlayRenderItem> Items { get; }



	public OverlayRenderSnapshot(IReadOnlyList<OverlayRenderItem> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		Items = items.ToArray();
	}

}



public sealed class OverlayRenderItem

{

	public required string ImageKey { get; init; }



	public int X { get; init; }



	public int Y { get; init; }



	public double Opacity { get; init; }



	public double Scale { get; init; }

}



