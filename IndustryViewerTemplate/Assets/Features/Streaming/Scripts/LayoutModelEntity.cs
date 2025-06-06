using UnityEngine;
using System;
using System.Collections.Generic;

namespace Unity.Industry.Viewer.Streaming
{
    public class LayoutJson
    {
        public List<LayoutModelEntity> LayoutModels;
        
        public LayoutJson(){}
        
        public LayoutJson(List<LayoutModelEntity> layoutModels)
        {
            LayoutModels = layoutModels;
        }
    }
    
    public class LayoutModelEntity : IEquatable<LayoutModelEntity>
    {
        public string orgID;
        public string projectID;
        public string assetID;
        public string datasetID;
        public string gameObjectName;
        public string versionID;
        public int version;
        public bool isStreaming;

        public float localPositionX;
        public float localPositionY;
        public float localPositionZ;

        public float localRotationX;
        public float localRotationY;
        public float localRotationZ;
        public float localRotationW;
        
        public LayoutModelEntity(){}

        public LayoutModelEntity(StreamingModel model)
        {
            orgID = model.OrgID;
            projectID = model.ProjectID;
            assetID = model.AssetId;
            versionID = model.VersionID;
            datasetID = model.Dataset != null? model.Dataset.Descriptor.DatasetId.ToString() : string.Empty;

            version = model.Version;
            
            gameObjectName = model.gameObject.name;
            
            isStreaming = model.IsStreaming;

            localPositionX = model.transform.localPosition.x;
            localPositionY = model.transform.localPosition.y;
            localPositionZ = model.transform.localPosition.z;

            localRotationX = model.transform.localRotation.x;
            localRotationY = model.transform.localRotation.y;
            localRotationZ = model.transform.localRotation.z;
            localRotationW = model.transform.localRotation.w;
        }
        
        public Vector3 GetLocalPosition()
        {
            return new Vector3(localPositionX, localPositionY, localPositionZ);
        }
        
        public Quaternion GetLocalRotation()
        {
            return new Quaternion(localRotationX, localRotationY, localRotationZ, localRotationW);
        }

        public bool Equals(LayoutModelEntity other)
        {
            if (other == null) return false;
            return orgID == other.orgID &&
                   projectID == other.projectID &&
                   assetID == other.assetID &&
                   versionID == other.versionID &&
                   gameObjectName == other.gameObjectName;
        }
        
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != GetType()) return false;
            return Equals(obj as LayoutModelEntity);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(orgID, projectID, assetID, versionID, gameObjectName);
        }
    }
}
