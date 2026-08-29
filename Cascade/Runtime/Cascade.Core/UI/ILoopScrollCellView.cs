namespace Cascade.Core
{
    public interface ILoopScrollCellView<in TItem>
    {
        void Bind(TItem item);
    }
}
