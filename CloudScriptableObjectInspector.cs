#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Magix
{
    public enum Environment
    {
        Production,
        Development
    }

    [CustomEditor(typeof(CloudScriptableObject), true)]
    [CanEditMultipleObjects]
    public class CloudResourceEditor : Editor
    {
        private readonly string[] _environmentOptions = new[] { Environment.Production.ToString(), Environment.Development.ToString() };
        private int _selectedEnvironmentIndex = 0;
        private bool _repaintRequested = false;
        private bool IsMultipleSelection => targets.Length > 1;
        private CloudScriptableObject CloudTarget => (CloudScriptableObject)target;
        private object CloudOrig => CloudTarget.Original;

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_repaintRequested)
            {
                Repaint();
                _repaintRequested = false;
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
                GUILayout.Label("Waiting resource to be ready...");
                Logger.LogVerbose("Resource trying to initialize...");

                targetResource.InitializeResource((success) =>
                {
                    if (!success)
                        Logger.LogError("An error occured while loading resource from cloud");

                    _repaintRequested = true;
                }, GetCurrentEnvironment());

                return true;
            }

            return false;
        }

        private bool WaitForLogin()
        {
            if (!InstanceManager.ResourceAPI.IsLoggedIn)
            {
                GUILayout.Label("Waiting cloud to be online");

                if (GUILayout.Button("Refresh"))
                {
                    InstanceManager.ResourceAPI.EditorLogin();
                }

                this.Repaint();
                return true;
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                GUILayout.Label("Controls are turned off in play mode");
                base.OnInspectorGUI();
                return;
            }

            if (IsMultipleSelection)
            {
                if (DoesTargetEligibleForUpload(targets) && GUILayout.Button($"Upload {targets.Length} objects."))
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
                                Debug.Log("Upload successfully");
                            }
                        });
                    }
                }

                base.OnInspectorGUI();
                return;
            }

            if (InstanceManager.ResourceAPI == null)
            {
                GUILayout.Label("Waiting InstanceManager.ResourceAPI to be initialized.");
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
                    CloudTarget.IsInitInProgress = true;

                    var originalPrototype = Activator.CreateInstance(target.GetType());
                    InstanceManager.ResourceAPI.GetVariableCloudJson(InstanceManager.Resolver.GetKeyFromObject((CloudScriptableObject)target,
                            GetCurrentEnvironment()),
                            target.GetType(),
                            (success, _, obj) =>
                    {
                        if (!success)
                        {
                            Logger.LogError("An error occured during fetching original from the cloud");
                        }

                        JsonUtility.FromJsonOverwrite(obj, originalPrototype);
                        CloudTarget.Original = originalPrototype;
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
                serializedObject.ApplyModifiedProperties();
            }
        }

        private bool RenderControls(CloudScriptableObject targetResource)
        {
            if (GUILayout.Button("Upload"))
            {
                int callbackCount = 0;
                foreach (var option in _environmentOptions)
                {
                    // Add rollback if one or more of the upload fails
                    InstanceManager.ResourceAPI.SetVariableCloud(InstanceManager.Resolver.GetKeyFromObject(targetResource,
                            (Environment)Enum.Parse(typeof(Environment), option)),
                            target,
                            (success, _) =>
                    {
                        if (!success)
                        {
                            Logger.LogError("An error occured while uploading resource from cloud");
                            return;
                        }

                        Logger.LogVerbose("Resource successfully uploaded to cloud");
                        callbackCount++;

                        if (callbackCount == _environmentOptions.Length)
                        {
                            Debug.Log("Tail success");
                            targetResource.IsExist = true;
                            _repaintRequested = true;
                        }
                    });
                }
            }

            return false;
        }
        private bool RenderResourceSyncOptions(CloudScriptableObject targetResource)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                if (targetResource.IsInitInProgress)
                    return false;
                else if (targetResource.IsExist)
                {
                    ReloadResource(targetResource);
                    return true;
                }

                return false;
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
            GUILayout.Label("Resource Collection: NakamaScriptableObj");
            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            _selectedEnvironmentIndex = EditorGUILayout.Popup("Environment", _selectedEnvironmentIndex, _environmentOptions);
            bool productionDropdownChanged = EditorGUI.EndChangeCheck();

            if (productionDropdownChanged)
            {
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
                Logger.Log("Pull success");
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

                    Logger.Log("Sync" + originalJson + " \n Current: " + currentJson);
                    if (originalJson != currentJson)
                    {
                        Logger.LogError("Original and current versions of the resource are not identical. A sync may be required.\n Original: " + originalJson + " \n Current: " + currentJson);
                        return;
                    }

                    InstanceManager.ResourceAPI.SetVariableCloud(InstanceManager.Resolver.GetKeyFromObject(targetResource, GetCurrentEnvironment()),
                            targetResource,
                            (success, message) =>
                    {
                        if (success)
                        {
                            targetResource.Original = original;
                        }
                        else
                        {
                            Logger.LogError("An error occurred while syncing the resource: " + message);
                        }

                        targetResource.IsInit = false;
                        targetResource.IsExist = false;
                        targetResource.Original = null;
                        _repaintRequested = true;
                    });
                }
            });
        }

        private void UploadResource(CloudScriptableObject targetToUpload, Action onComplete)
        {
            int ctx = 0;
            foreach (var option in _environmentOptions)
            {
                InstanceManager.ResourceAPI.SetVariableCloud(InstanceManager.Resolver.GetKeyFromObject((CloudScriptableObject)targetToUpload,
                                                    (Environment)Enum.Parse(typeof(Environment), option)),
                                                    targetToUpload,
                                                    (success, message) =>
                {
                    if (!success)
                    {
                        Logger.LogError("An error occured while uploading resource from cloud");
                        return;
                    }

                    Logger.LogVerbose("Resource successfully uploaded to cloud");

                    targetToUpload.IsExist = true;

                    ctx++;
                    if (ctx == _environmentOptions.Length)
                        onComplete.Invoke();
                });
            }
        }

        private void GetOriginalResource(CloudScriptableObject targetResource, Action<object> callback)
        {
            var originalPrototype = Activator.CreateInstance(targetResource.GetType());
            InstanceManager.ResourceAPI.GetVariableCloudJson(InstanceManager.Resolver.GetKeyFromObject(targetResource, GetCurrentEnvironment()),
                    targetResource.GetType(),
                    (success, message, obj) =>
            {
                if (!success)
                {
                    Logger.LogError("An error occured during fetching original from the cloud");
                }

                JsonUtility.FromJsonOverwrite(obj, originalPrototype);
                callback.Invoke(originalPrototype);
            });
        }

        private ReorderableList CreateReorderableList(SerializedProperty property)
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

                         if (current.IsTypePrimitive())
                         {
                             if (!IsPropertyPresentInRemote(current))
                             {
                                 DrawHiglighMark(current, rect.x + current.depth * 8, elementStartPosY, Color.red);
                             }
                             else if (IsPropertyDifferentThanOriginal(current,CloudOrig , out var originalValue))
                             {
                                 DrawHiglighMark(current, rect.x + current.depth * 8, elementStartPosY, Color.green);
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
                    ReorderableList list = CreateReorderableList(property);
                    serializedObject.Update();
                    list.DoLayoutList();
                    serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    Rect position = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, true));

                    if (property.IsTypePrimitive() && IsPropertyDifferentThanOriginal(property,CloudOrig, out var originalvalue))
                    {
                        DrawHiglighMark(property, position.x, position.y, Color.green);
                        AttachContextMenu(position, property, originalvalue);
                    }

                    EditorGUI.PropertyField(position, property, true);
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

                if (property.IsTypePrimitive() && IsPropertyDifferentThanOriginal(property, CloudOrig,out var originalvalue))
                {
                    property.SetValue(originalvalue);
                }
            }
        }

        private bool DoesTargetEligibleForUpload(object[] targets)
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


        void DrawHiglighMark(SerializedProperty field, float x, float y, Color markColor)
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

        private static object GetTargetObjectOfProperty(SerializedProperty prop, object obj)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++) // Traverse to the parent of the last element
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

            // Get the value of the last property
            string lastElement = elements.Last();
            if (lastElement.Contains("["))
            {
                var elementName = lastElement.Substring(0, lastElement.IndexOf("["));
                var index = Convert.ToInt32(lastElement.Substring(lastElement.IndexOf("[")).Replace("[", "").Replace("]", ""));
                return GetValueFromIndex(obj, elementName, index);
            }
            else
            {
                return GetValueFromField(obj, lastElement);
            }
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

        private string GetPrefix(string str)
        {
            bool success = Enum.TryParse(typeof(Environment), str, true, out var obj);
            if (!success)
                throw new Exception("Unkown enum value while parsing");
            return obj.ToString() + "-";
        }
    }

}
#endif

