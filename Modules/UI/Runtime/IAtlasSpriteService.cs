using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using Cascade.Service;
namespace Cascade.Service
{
    /// <summary>
    /// Loads sprites packed into generated SpriteAtlas assets through the
    /// AtlasMapping index (spriteName -&gt; atlasName). Atlas handles are cached
    /// for the service lifetime so callers do not need to hold their own handle.
    /// </summary>
    public interface IAtlasSpriteService
    {
        /// <summary>
        /// Loads a sprite from its generated atlas. Returns null when the sprite is
        /// not part of any generated UI atlas or when loading fails.
        /// </summary>
        UniTask<Sprite> LoadSpriteAsync(string spriteName, CancellationToken cancellationToken = default);

        /// <summary>True when the sprite is packed in a generated UI atlas and the index is loaded.</summary>
        bool TryGetAtlasName(string spriteName, out string atlasName);

        /// <summary>Releases all cached atlas handles and forgets the index.</summary>
        void Reset();
    }
}