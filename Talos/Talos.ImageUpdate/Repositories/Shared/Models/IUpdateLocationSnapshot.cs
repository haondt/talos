namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public interface IUpdateLocationSnapshot
    {
        bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot);
    }



}
