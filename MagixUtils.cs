#if UNITY_EDITOR
using UnityEditor;

namespace Magix
{
    public static class MagixUtils
    {
        public static string GetFullName(CloudScriptableObject obj, Environment env)
        {
			string assetPath = AssetDatabase.GetAssetPath(obj);
			assetPath = assetPath.Replace("Assets/Resources/", string.Empty);
			assetPath = assetPath.Replace(".asset", string.Empty);
			return $"{env}-res-{assetPath}";
        }
    }
}
#endif
