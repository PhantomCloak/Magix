using System.Collections.Generic;
using Nakama;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json;
using Magix.Diagnostics;

namespace Magix.Adapter
{
    public class DefaultNakamaAdapter : ICloudResourceAPI
    {
        private static Client client = null;
        private static ISession session = null;

        private const string scheme = "http";
        private const string host = "ec2-3-67-226-142.eu-central-1.compute.amazonaws.com";
        private const int port = 7350;
        private const string serverKey = "defaultkey";

        private bool m_IsLoggedIn = false;
        public bool IsLoggedIn { get => m_IsLoggedIn; set => m_IsLoggedIn = value; }
        public string EditorUserId => "00d5464a-290e-40d5-882f-7d9e54cae3eb";

        public DefaultNakamaAdapter()
        {

        }

        public static void Init(Action<bool, string> onInitCallback)
        {
            Task.Run(() =>
            {
                if (client != null)
                {
                    MagixLogger.LogWarn($"Client already initialized");
                    return Task.CompletedTask;
                }

                try
                {
                    client = new Client(scheme, host, port, serverKey);
                    onInitCallback.Invoke(true, string.Empty);
                }
                catch (Exception e)
                {
                    onInitCallback.Invoke(false, e.ToString());
                }

                return Task.CompletedTask;
            });
        }

        public void Login(string userName, Action<bool, string> onLoginCallback = null)
        {
            Task.Run(async () =>
            {
                try
                {
                    session = await client.AuthenticateDeviceAsync(userName);

                    onLoginCallback.Invoke(true, string.Empty);
                }
                catch (Exception e)
                {
                    onLoginCallback.Invoke(false, e.ToString());
                }
            });
        }

        private static bool LoginLock = false;
        public void EditorLogin(Action onEditorLogin = null)
        {
            if (LoginLock)
            {
                return;
            }

            LoginLock = true;
            client = new Client(scheme, host, port, serverKey);

            Login("unity-engine", (success, message) =>
            {
                LoginLock = false;
                if (!success)
                {
                    MagixLogger.LogError("An error occured while editor login: " + message);
                    return;
                }

                MagixLogger.Log("Editor login success");
                IsLoggedIn = true;
                onEditorLogin?.Invoke();
            });
        }

        public void CheckVariableIsExist(string userId, string resourceName, Action<bool> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var objectId = new StorageObjectId
                    {
                        Collection = "default",
                        Key = resourceName,
                        UserId = session.UserId
                    };

                    var result = await client.ListUsersStorageObjectsAsync(session, "default", userId, 100);

                    callback.Invoke(result.Objects.Any(x => x.Key == resourceName));
                }
                catch (Exception e)
                {
                    MagixLogger.LogError("An error occured while checking the variable is exist. exception: " + e);
                }
            });
        }

        public void GetVariableCloudJson(string variableName, Type type, Action<bool, string> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var objectId = new StorageObjectId
                    {
                        Collection = "default",
                        Key = variableName,
                        UserId = session.UserId
                    };

                    var result = await client.ReadStorageObjectsAsync(session, new[] { objectId });

                    if (result.Objects.Count() == 0)
                    {
                        callback.Invoke(false, string.Empty);
                        return;
                    }

                    callback.Invoke(true, result.Objects.Select(x => x.Value).FirstOrDefault());
                }
                catch (Exception e)
                {
                    MagixLogger.LogError("An error occured while getting the variable. exception: " + e);
                    callback.Invoke(false, string.Empty);
                }
            });

        }

        public void SetVariableCloud<T>(string variableName, T obj, Action<bool> callback = null)
        {
            var objStr = JsonConvert.SerializeObject(obj);
            Task.Run(async () =>
           {
               try
               {
                   var writeObject = new WriteStorageObject
                   {
                       Collection = "default",
                       Key = variableName,
                       Value = objStr,
                       PermissionRead = 2, // Public read
                       PermissionWrite = 1 // Owner write
                   };

                   await client.WriteStorageObjectsAsync(session, new[] { writeObject });

                   callback?.Invoke(true);
               }
               catch (Exception e)
               {
                   MagixLogger.LogError("An error occured while setting the variable. exception: " + e);
                   callback?.Invoke(false);
               }
           });
        }

        public void GetAllEntriesUser(string userName, Action<Dictionary<string, string>> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var objects = await client.ListUsersStorageObjectsAsync(session, "default", userName, 100);
                    var dictionary = objects.Objects.ToDictionary(obj => obj.Key, obj => obj.Value);

                    callback.Invoke(dictionary);
                }
                catch (Exception e)
                {
                    MagixLogger.LogError("An error occured while setting the variable. exception: " + e);
                    callback.Invoke(new Dictionary<string, string>());
                }
            });
        }

        public void DeleteVariableCloud(string variableName, Action<bool> callback = null)
        {
            Task.Run(async () =>
           {
               try
               {
                   var storageObjectIds = new List<StorageObjectId>
                    {
                    new StorageObjectId
                    {
                        Collection = "default",
                        Key = variableName,
                    }
                    };

                   await client.DeleteStorageObjectsAsync(session, storageObjectIds.ToArray());

                   callback.Invoke(true);
               }
               catch (Exception e)
               {
                   MagixLogger.LogError("An error occured while deleting the variable. exception: " + e);
                   callback.Invoke(false);
               }
           });
        }
    }
}
