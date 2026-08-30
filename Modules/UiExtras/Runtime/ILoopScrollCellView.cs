namespace Cascade.Modules.UIExtras
{
    public interface ILoopScrollCellView<in TItem>
    {
        void Bind(TItem item);
    }

    /// <summary>
    /// Optional reset contract for pooled list cells.
    /// LoopScrollListBinder calls Clear before Bind and when returning a cell to the pool.
    /// </summary>
    public interface IResettableLoopScrollCellView
    {
        void Clear();
    }
}
