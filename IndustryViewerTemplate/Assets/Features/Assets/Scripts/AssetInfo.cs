using System;
using Unity.Cloud.Assets;

namespace Unity.Industry.Viewer.Assets
{
    public struct AssetInfo : IEquatable<AssetInfo> {
        public IAsset Asset;
        public AssetProperties? Properties;
        
        public override bool Equals(object obj)
        {
            return obj is AssetInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Asset.Descriptor.GetHashCode();
        }

        public bool Equals(AssetInfo other)
        {
            return Equals(Asset.Descriptor.OrganizationId, other.Asset.Descriptor.OrganizationId) &&
                   Equals(Asset.Descriptor.ProjectId, other.Asset.Descriptor.ProjectId) &&
                   Equals(Asset.Descriptor.AssetId, other.Asset.Descriptor.AssetId) &&
                   Equals(Asset.Descriptor.AssetVersion, other.Asset.Descriptor.AssetVersion);
        }
        
        public static bool operator ==(AssetInfo left, AssetInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssetInfo left, AssetInfo right)
        {
            return !left.Equals(right);
        }
    }
}
