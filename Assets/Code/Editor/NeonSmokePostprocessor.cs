using UnityEditor;
using UnityEngine;
using System.IO;

public class NeonSmokePostprocessor : AssetPostprocessor
{
    // What scale to use (1.0 = same as Maya)
    const float scale = 1.0f;

    void OnPreprocessModel()
    {
        ModelImporter importer = assetImporter as ModelImporter;
        
        //importer.animationType = ModelImporterAnimationType.None;
        //importer.generateAnimations = ModelImporterGenerateAnimations.None;
        //importer.importAnimation = false;
        importer.useFileScale = false;
        importer.globalScale = scale;
        importer.importMaterials = false;

        if (assetPath.Contains("@"))
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                AnimationClip copiedClip = new AnimationClip();


                string path = AssetDatabase.GetAssetPath(sourceClip);
                path = Path.Combine(Path.GetDirectoryName(path), sourceClip.name) + ".anim";

                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);

                    EditorUtility.CopySerialized(sourceClip, copiedClip);
                    AssetDatabase.CreateAsset(copiedClip, path);
                }
                else
                {
                    EditorUtility.CopySerialized(sourceClip, copiedClip);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
