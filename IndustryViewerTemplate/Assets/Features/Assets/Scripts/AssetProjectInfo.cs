using System;
using Unity.Cloud.Assets;

namespace Unity.Industry.Viewer.Assets
{
    public struct AssetProjectInfo : IEquatable<AssetProjectInfo> {
        public IAssetProject AssetProject;
        public AssetProjectProperties? Properties;
        
        public override bool Equals(object obj)
        {
            return obj is AssetProjectInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return AssetProject.GetHashCode();
        }
        
        public bool Equals(AssetProjectInfo other)
        {
            return Equals(AssetProject.Descriptor, other.AssetProject.Descriptor);
        }
        
        public static bool operator ==(AssetProjectInfo left, AssetProjectInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssetProjectInfo left, AssetProjectInfo right)
        {
            return !left.Equals(right);
        }
    }
}
