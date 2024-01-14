using System;
using System.Collections.Generic;

namespace Magix
{
    public interface ICloudResourceAPI
    {
        public void EditorLogin(Action onEditorLogin = null);

        public bool IsLoggedIn { set; get; }

        public string EditorUserId { get; }

        // Success, Result
        public void CheckVariableIsExist(string resourceName, Action<bool, bool> callback);
        // Success, ErrorMessage, Result
        public void GetVariableCloudJson(string variableName, Type type, Action<bool, string, string> onCompleteCallback);
        // Success, ErrorMessage, Result
        public void SetVariableCloud<T>(string variableName, T obj, Action<bool, string> onCompleteCallback = null);
        // Success, Result
        public void GetAllEntriesUser(string userName, Action<bool, Dictionary<string, string>> onCompleteCallback);
        // Succes, Message
        public void DeleteVariableCloud(string variableName, Action<bool, string> onCompleteCallback = null);
    }
}
