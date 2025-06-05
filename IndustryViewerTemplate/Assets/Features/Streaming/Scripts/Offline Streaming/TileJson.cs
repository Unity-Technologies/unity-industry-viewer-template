using System;
using System.Collections.Generic;

namespace Unity.Industry.Viewer.Streaming
{
    [Serializable]
    public struct TileJson
    {
        [Serializable]
        public struct Asset
        {
            public string version;
            public Extras extras;
        }

        [Serializable]
        public struct Extras
        {
            public string syncServiceVersion;
        }

        [Serializable]
        public struct ExtensionsInfo
        {
            public List<string> extensionsUsed;
            public List<string> extensionsRequired;
        }

        [Serializable]
        public struct Extensions
        {
            public ExtensionsInfo _3DTILES_content_gltf;
        }

        [Serializable]
        public struct BoundingVolume
        {
            public List<double> box;
        }

        [Serializable]
        public struct Content
        {
            public string uri;
        }

        [Serializable]
        public struct Geometry
        {
            public BoundingVolume boundingVolume;
            public Content content;
            public double geometricError;
            public string refine;
            public List<Geometry> children;
        }

        public Asset asset;
        public Extensions extensions;
        public List<string> extensionsUsed;
        public List<string> extensionsRequired;
        public double geometricError;
        public Geometry root;
    }
}
