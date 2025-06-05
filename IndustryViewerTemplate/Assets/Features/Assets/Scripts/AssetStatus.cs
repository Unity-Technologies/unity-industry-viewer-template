using UnityEngine;
using System;
using System.Linq;

namespace Unity.Industry.Viewer.Assets
{
    public enum AssetStatus
    {
        Draft,
        Inreview,
        Approved,
        Rejected,
        Published,
        Withdrawn
    }
    
    public static class AssetStatusExtensions
    {
        public static string GetValueAsString(this AssetStatus status, bool forSubmit)
        {
            return status switch
            {
                AssetStatus.Draft => "Draft",
                AssetStatus.Inreview => forSubmit? "InReview" : "In review",
                AssetStatus.Approved => "Approved",
                AssetStatus.Rejected => "Rejected",
                AssetStatus.Published => "Published",
                AssetStatus.Withdrawn => "Withdrawn",
                _ => string.Empty
            };
        }
        
        public static AssetStatus GetAssetStatusFromString(this string value)
        {
            return value switch
            {
                "Draft" => AssetStatus.Draft,
                "In review" => AssetStatus.Inreview,
                "InReview" => AssetStatus.Inreview,
                "Approved" => AssetStatus.Approved,
                "Rejected" => AssetStatus.Rejected,
                "Published" => AssetStatus.Published,
                "Withdrawn" => AssetStatus.Withdrawn,
                _ => AssetStatus.Draft
            };
        }

        public static Color ReturnStatusColor(this AssetStatus status)
        {
            return status switch
            {
                AssetStatus.Draft => new Color(1f, 189f / 255f, 53f / 255f),
                AssetStatus.Inreview => new Color(13 / 255f, 99f / 255f, 193f / 255f),
                AssetStatus.Approved => new Color(42f / 255f, 154f / 255f, 97f / 255f),
                AssetStatus.Rejected => new Color(225f / 255f, 63f / 255f, 68f / 255f),
                AssetStatus.Published => new Color(73f / 255f, 176f / 255f, 161f / 255f),
                AssetStatus.Withdrawn => new Color(246f / 255f, 96f / 255f, 21f / 255f),
                _ => Color.white
            };
        }
        
        public static AssetStatus[] GetAssetStatuses()
        {
            return Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>().ToArray();
        }
    }
}
