#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Magix.Diagnostics;
namespace Magix.Editor
{
    public enum Environment
    {
        Production,
        Development
    }

    [CustomEditor(typeof(CloudScriptableObject), true)]
    [CanEditMultipleObjects]
    public class CloudResourceEditor : UnityEditor.Editor
    {
        private readonly string[] _environmentOptions = { Environment.Production.ToString(), Environment.Development.ToString() };
        private int _selectedEnvironmentIndex = 0;
        private bool _repaintRequested = false;
        private bool _reloadRequested = false;
        private bool IsMultipleSelection => targets.Length > 1;
        private CloudScriptableObject CloudTarget => (CloudScriptableObject)target;
        private object CloudOrig => CloudTarget.Original;

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                InstanceManager.ResourceAPI.IsLoggedIn = false;
                InstanceManager.ResourceAPI.EditorLogin(() =>
                {
                    _reloadRequested = true;
                });
            }
        }

        private void OnEditorUpdate()
        {
            if (_repaintRequested)
            {
                _repaintRequested = false;
            }

            if (_reloadRequested)
            {
                Repaint();
                Debug.Log("C");
                ReloadResource((CloudScriptableObject)target);
                _reloadRequested = false;
            }
        }

        private bool WaitResourceInit(CloudScriptableObject targetResource)
        {
            if (targetResource.IsInitInProgress)
            {
                return true;
            }

            if (!targetResource.IsInit)
            {
                GUILayout.Label($"Initializing resource '{targetResource.name}'");

                MagixLogger.LogVerbose($"Resource '{targetResource.name}' is trying to initialize for environment: {GetCurrentEnvironment()}");

                var targetName = targetResource.name;

                // While the way using the success might not be the ideal
                // It's easier alternative to do additional checks 
                // If something going to happen always can use log
                targetResource.InitializeResource((success) =>
                {
                    targetResource.IsExist = success;

                    if (success)
                    {
                        _reloadRequested = true;
                        MagixLogger.LogVerbose("CloudResource " + targetName + " exist on cloud.");
                    }
                    else
                    {
                        _repaintRequested = true;
                        MagixLogger.LogVerbose("CloudResource " + targetName + " does not exist on cloud.");
                    }

                    //MagixLogger.LogError($"Failed to load resource '{targetName}' from cloud. Environment: {GetCurrentEnvironment()}");

                }, GetCurrentEnvironment());

                return true;
            }

            return false;
        }

        private bool WaitForLogin()
        {
            if (!InstanceManager.ResourceAPI.IsLoggedIn)
            {
                GUILayout.Label("Awaiting login to cloud services.");
                MagixLogger.LogVerbose("Awaiting login...");

                if (GUILayout.Button("Retry Login"))
                {
                    MagixLogger.LogVerbose("Attempting to log in through the editor...");
                    InstanceManager.ResourceAPI.EditorLogin(() =>
                    {
                        _reloadRequested = true;
                    });
                }

                return true;
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                GUILayout.Label("Editor controls are disabled during play mode.");
                base.OnInspectorGUI();
                return;
            }

            if (IsMultipleSelection)
            {
                if (IsTargetsCloudCompatible(targets) && GUILayout.Button($"Upload {targets.Length} objects."))
                {
                    int ctx = 0;
                    foreach (var targetToUpload in targets)
                    {
                        UploadResource((CloudScriptableObject)targetToUpload, () =>
                        {
                            ctx++;
                            if (ctx == targets.Length)
                            {
                                _repaintRequested = true;
                            }
                        });
                    }
                }

                base.OnInspectorGUI();
                return;
            }

            if (InstanceManager.ResourceAPI == null)
            {
                GUILayout.Label("Initializing InstanceManager.ResourceAPI...");
                MagixLogger.LogVerbose("Initializing InstanceManager.ResourceAPI...");
                GUILayout.Space(10);
                base.OnInspectorGUI();
                return;
            }

            if (WaitForLogin())
                return;

            if (WaitResourceInit(CloudTarget))
                return;

            GUILayout.Space(25);

            EditorGUI.BeginChangeCheck();
            if (CloudTarget.IsExist)
            {
                if (!IsMultipleSelection && RenderResourceInfo(CloudTarget))
                {
                    return;
                }
                if (!IsMultipleSelection && RenderResourceSyncOptions(CloudTarget))
                {
                    return;
                }

                if (CloudTarget.IsInitInProgress)
                {
                    return;
                }

                if (CloudTarget.Original == null)
                {
                    MagixLogger.LogVerbose("Fetching original copy of " + CloudTarget.name + " from cloud.");
                    CloudTarget.IsInitInProgress = true;
                    GetOriginalResource((CloudScriptableObject)target, (obj) =>
                    {
                        CloudTarget.Original = obj;
                        CloudTarget.IsInitInProgress = false;
                    });
                }
            }
            else
            {
                if (!IsMultipleSelection)
                    RenderControls(CloudTarget);
            }

            GUILayout.Space(25);

            DrawResource(target);

            if (EditorGUI.EndChangeCheck())
            {
                //serializedObject.ApplyModifiedProperties();
            }
        }

        private bool RenderControls(CloudScriptableObject targetResource)
        {
            if (GUILayout.Button("Upload"))
            {
                UploadResource(targetResource, () =>
                {
                    targetResource.IsExist = true;
                    _repaintRequested = true;
                });
            }

            return false;
        }

        private bool RenderResourceSyncOptions(CloudScriptableObject targetResource)
        {
            GUILayout.BeginHorizontal();

            Event e = Event.current;

            if (!e.shift && GUILayout.Button("Refresh"))
            {
                if (targetResource.IsInitInProgress)
                    return false;
                else if (targetResource.IsExist)
                {
                    Debug.Log("B");
                    ReloadResource(targetResource);
                    return true;
                }

                return false;
            }
            else if (e.shift)
            {
                if (GUILayout.Button("De-attach from cloud"))
                {
                    int ctx = 0;
                    foreach (var env in MagixConfig.Environments)
                    {
                        InstanceManager.ResourceAPI.DeleteVariableCloud(env + "-res-" + targetResource.name, (success) =>
                        {
                            ctx++;
                            if (ctx >= MagixConfig.Environments.Length)
                            {
                                MagixLogger.LogVerbose("Cloud resource deleted successfully");
                                targetResource.IsExist = false;
                                targetResource.IsInit = false;
                                _repaintRequested = true;
                            }
                        });
                    }
                }
            }

            bool isOnProduction = (Environment)Enum.Parse(typeof(Environment), _environmentOptions[_selectedEnvironmentIndex]) == Environment.Production;

            var buttonTextStyle = new GUIStyle(GUI.skin.button);
            buttonTextStyle.normal.textColor = isOnProduction ? Color.red : Color.white;

            if (GUILayout.Button("Push to cloud", buttonTextStyle))
            {
                if (isOnProduction)
                {
                    bool uploadConset = EditorUtility.DisplayDialog(
                        "WARNING",
                        $"You are about upload CloudResource: {targetResource.name} to PRODUCTION",
                        "Yes",
                        "No"
                    );

                    if (!uploadConset)
                    {
                        return false;
                    }

                }

                PushChanges(targetResource);
                return true;
            }

            GUILayout.EndHorizontal();
            return false;
        }

        private bool RenderResourceInfo(CloudScriptableObject targetResource)
        {
            GUILayout.Label($"Resource User Id: {InstanceManager.ResourceAPI.EditorUserId}");
            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            _selectedEnvironmentIndex = EditorGUILayout.Popup("Environment", _selectedEnvironmentIndex, _environmentOptions);
            bool productionDropdownChanged = EditorGUI.EndChangeCheck();

            if (productionDropdownChanged)
            {
                Debug.Log("A");
                ReloadResource(targetResource);
                return true;
            }

            GUILayout.Space(10);

            return false;
        }

        private void ReloadResource(CloudScriptableObject targetResource)
        {
            targetResource.IsInitInProgress = true;
            PullChanges(target, () =>
            {
                targetResource.IsInitInProgress = false;
                _repaintRequested = true;
                ResetResources();
            });
        }

        private void PullChanges(object targetResource, Action callback)
        {
            GetOriginalResource((CloudScriptableObject)targetResource, (original) =>
            {
                EditorApplication.update += CompareAndSync;

                // Cool hack innit?
                void CompareAndSync()
                {
                    EditorApplication.update -= CompareAndSync;

                    string originalJson = JsonUtility.ToJson(original);
                    string currentJson = JsonUtility.ToJson(target);

                    // if (originalJson != currentJson)
                    {
                        EditorUtility.SetDirty(target);

                        JsonUtility.FromJsonOverwrite(originalJson, target);

                        AssetDatabase.SaveAssetIfDirty(target);
                    }

                    ((CloudScriptableObject)target).Original = original;

                    callback.Invoke();
                }
            });
        }

        private void PushChanges(CloudScriptableObject targetResource)
        {
            GetOriginalResource(targetResource, (original) =>
            {
                EditorApplication.update += CompareAndSync;
                void CompareAndSync()
                {
                    EditorApplication.update -= CompareAndSync;

                    string originalJson = JsonUtility.ToJson(original);
                    string currentJson = JsonUtility.ToJson(targetResource.Original);

                    if (originalJson != currentJson)
                    {
                        MagixLogger.LogError("Original and current versions of the resource are not identical. A sync may be required.\n Original: " + originalJson + " \n Current: " + currentJson);
                        return;
                    }

                    InstanceManager.ResourceAPI.SetVariableCloud(InstanceManager.Resolver.GetKeyFromObject(targetResource, GetCurrentEnvironment()),
                            targetResource,
                            (success) =>
                    {
                        if (success)
                        {
                            targetResource.Original = original;
                        }
                        else
                        {
                            MagixLogger.LogError("An error occurred while syncing the resource");
                        }

                        targetResource.IsInit = false;
                        targetResource.IsExist = false;
                        targetResource.Original = null;
                        _repaintRequested = true;
                    });
                }
            });
        }

        private void UploadResource(CloudScriptableObject targetToUpload, Action onComplete = null)
        {
            int ctx = 0;
            foreach (var option in _environmentOptions)
            {
                InstanceManager.ResourceAPI.SetVariableCloud(InstanceManager.Resolver.GetKeyFromObject(targetToUpload,
                                                    (Environment)Enum.Parse(typeof(Environment), option)),
                                                    targetToUpload,
                                                    (success) =>
                {
                    if (!success)
                    {
                        MagixLogger.LogError("An error occured while uploading resource from cloud");
                        return;
                    }

                    MagixLogger.LogVerbose("Resource successfully uploaded to cloud");

                    targetToUpload.IsExist = true;

                    ctx++;
                    if (ctx == _environmentOptions.Length)
                        onComplete?.Invoke();
                });
            }
        }

        private void GetOriginalResource(CloudScriptableObject targetResource, Action<object> callback)
        {
            var key = InstanceManager.Resolver.GetKeyFromObject(targetResource, GetCurrentEnvironment());

            var originalPrototype = Activator.CreateInstance(targetResource.GetType());
            InstanceManager.ResourceAPI.GetVariableCloudJson(key,
                    targetResource.GetType(),
                    (success, objStr) =>
            {
                if (!success)
                {
                    MagixLogger.LogError("An error occured during fetching original from the cloud with key: " + key);
                }

                JsonUtility.FromJsonOverwrite(objStr, originalPrototype);
                callback.Invoke(originalPrototype);
            });
        }

        private ReorderableList DrawWithReorderableList(SerializedProperty property)
        {
            var list = new ReorderableList(property.serializedObject, property, true, true, true, true);

            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"{property.displayName} (Size: {property.arraySize}");

            list.drawElementCallback = (rect, index, _, _) =>
             {
                 SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);

                 rect.y += 2;
                 rect.x += 10;
                 rect.width -= 10;

                 Rect labelRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                 EditorGUI.LabelField(labelRect, $"Element {index}");

                 float elementStartPosY = rect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                 EditorGUI.PropertyField(
                     new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                     element, GUIContent.none, true
                 );

                 SerializedProperty current = element;
                 SerializedProperty end = current.GetEndProperty();

                 if (CloudTarget.IsExist)
                 {
                     float tippingPoint = float.MaxValue;
                     while (current.NextVisible(current.isExpanded) && !SerializedProperty.EqualContents(current, end))
                     {
                         if (tippingPoint < elementStartPosY)
                         {
                             elementStartPosY += 25;
                             tippingPoint = float.MaxValue;
                         }

                         if (current.IsTypeSerializeable())
                         {
                             if (!IsPropertyPresentInRemote(current))
                             {
                                 DrawHiglighMarkOnField(current, rect.x + current.depth * 8, elementStartPosY, Color.red);
                             }
                             else if (IsPropertyDifferentThanOriginal(current, CloudOrig, out var originalValue))
                             {
                                 DrawHiglighMarkOnField(current, rect.x + current.depth * 8, elementStartPosY, Color.green);
                                 AttachContextMenu(new Rect(rect.x - 5, elementStartPosY + 1, rect.width, EditorGUI.GetPropertyHeight(current, true) - 2), current.Copy(), originalValue);
                             }
                         }

                         if (current.isArray && current.isExpanded)
                         {
                             elementStartPosY += 6;
                             tippingPoint = elementStartPosY + EditorGUI.GetPropertyHeight(current, true) - EditorGUIUtility.singleLineHeight;
                         }
                         else
                             elementStartPosY += (current.hasChildren ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(current, true)) + EditorGUIUtility.standardVerticalSpacing;
                     }
                 }

             };

            list.elementHeightCallback = index => EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), true) + 4;

            return list;
        }

        private void DrawResource(object targetObject)
        {
            var fields = targetObject.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                SerializedProperty property = serializedObject.FindProperty(field.Name);

                if (property == null)
                    continue;

                if (field.FieldType.IsArray || (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    ReorderableList list = DrawWithReorderableList(property);
                    //serializedObject.Update();
                    list.DoLayoutList();
                    //serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    Rect position = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, true));

                    EditorGUI.PropertyField(position, property, true);
                    if (property.IsTypeSerializeable() && IsPropertyDifferentThanOriginal(property, ((CloudScriptableObject)targetObject).Original, out var originalValue))
                    {
                        EditorUtility.SetDirty(target);
                        DrawHiglighMarkOnField(property, position.x, position.y, Color.green);
                        AttachContextMenu(position, property, originalValue);
                    }
                }
            }

            //serializedObject.ApplyModifiedProperties();
        }

        private void ResetResources()
        {
            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                SerializedProperty property = serializedObject.FindProperty(field.Name);
                if (property == null)
                    continue;

                if (property.IsTypeSerializeable() && IsPropertyDifferentThanOriginal(property, CloudOrig, out var originalvalue))
                {
                    property.SetValue(originalvalue);
                }
            }
        }

        private static bool IsTargetsCloudCompatible(object[] targets)
        {
            int ctx = 0;
            foreach (var targetToInspect in targets)
            {
                if (targetToInspect.GetType().IsSubclassOf(typeof(CloudScriptableObject)) && !((CloudScriptableObject)targetToInspect).IsExist)
                    ctx++;
            }

            return ctx == targets.Length;
        }

        private void AttachContextMenu(Rect position, SerializedProperty property, object originalValue)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && position.Contains(currentEvent.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy"), false, () => GUIUtility.systemCopyBuffer = property.GetValue().ToString());
                menu.AddItem(new GUIContent("Revert"), false, () => property.SetValue(originalValue));
                menu.ShowAsContext();
                currentEvent.Use();
            }
        }

        void DrawHiglighMarkOnField(SerializedProperty field, float x, float y, Color markColor)
        {
            Rect highlightRect = new Rect(x - 5, y + 1, 1, EditorGUI.GetPropertyHeight(field, true) - 2);
            EditorGUI.DrawRect(highlightRect, markColor);
        }

        private bool IsPropertyPresentInRemote(SerializedProperty property)
        {
            var orig = ((CloudScriptableObject)target).Original;

            if (orig == null)
            {
                return false;
            }

            var info = GetFieldInfoFromProperty(property);

            if (info == null)
            {
                return false;
            }

            var objTargetRoot = GetTargetObjectOfPropertyParent(property, orig);

            if (objTargetRoot == null)
            {
                return false;
            }

            object originalValue = info.GetValue(objTargetRoot);

            return originalValue != null;
        }

        private static bool IsPropertyDifferentThanOriginal(SerializedProperty property, object orig, out object origValue)
        {
            var info = GetFieldInfoFromProperty(property);
            var objTargetRoot = GetTargetObjectOfPropertyParent(property, orig);
            object originalValue = null;

            var fieldIsEnum = info.FieldType.IsEnum;
            if (orig != null)
            {
                if (fieldIsEnum)
                    originalValue = (int)info.GetValue(objTargetRoot);
                else
                    originalValue = info.GetValue(objTargetRoot);
            }

            origValue = originalValue;
            var propValue = fieldIsEnum ? (int)property.GetValue() : property.GetValue();
            return !System.Object.Equals(originalValue, propValue) && originalValue != null;
        }

        private static FieldInfo GetFieldInfoFromProperty(SerializedProperty property)
        {
            Type objectType = property.serializedObject.targetObject.GetType();
            string propertyPath = property.propertyPath.Replace(".Array.data[", "[");

            string[] fieldStructure = propertyPath.Split('.');
            FieldInfo fieldInfo = null;

            for (int i = 0; i < fieldStructure.Length; i++)
            {
                string fieldName = fieldStructure[i];
                if (fieldName.Contains("["))
                {
                    fieldName = fieldName.Substring(0, fieldName.IndexOf('['));
                    fieldInfo = objectType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo == null) return null;

                    if (i < fieldStructure.Length - 1)
                    {
                        // For arrays
                        if (fieldInfo.FieldType.IsArray)
                        {
                            objectType = fieldInfo.FieldType.GetElementType();
                        }
                        // For generic lists
                        else if (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            objectType = fieldInfo.FieldType.GetGenericArguments()[0];
                        }
                    }
                }
                else
                {
                    fieldInfo = objectType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo == null) return null;

                    if (i < fieldStructure.Length - 1)
                    {
                        objectType = fieldInfo.FieldType;
                    }
                }
            }

            return fieldInfo;
        }

        private static object GetTargetObjectOfPropertyParent(SerializedProperty prop, object obj)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++) // Stop before the last element
            {
                var element = elements[i];
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValueFromIndex(obj, elementName, index);
                }
                else
                {
                    obj = GetValueFromField(obj, element);
                }
            }
            return obj;
        }

        private static object GetValueFromField(object source, string name)
        {
            if (source == null) return null;
            var type = source.GetType();
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return null;

            return f.GetValue(source);
        }

        private static object GetValueFromIndex(object source, string name, int index)
        {
            var enumerable = GetValueFromField(source, name) as IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        private Environment GetCurrentEnvironment()
        {
            return (Environment)Enum.Parse(typeof(Environment), _environmentOptions[_selectedEnvironmentIndex]);
        }
    }
}
#endif

