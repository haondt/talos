using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public record AtomicUpdateLocationSnapshot : IUpdateLocationSnapshot
    {
        public List<ISubatomicUpdateLocationSnapshot> Children { get; init; } = [];

        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not AtomicUpdateLocationSnapshot other)
                return false;
            if (Children.Count != other.Children.Count)
                return false;
            return Children.Zip(other.Children)
                .All(x => x.First.IsEquivalentTo(x.Second));
        }
    }
}
