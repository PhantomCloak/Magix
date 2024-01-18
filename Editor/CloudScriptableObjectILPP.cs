using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

public class CloudScriptableObjectILPP : ILPostProcessor
{
    ILPostProcessorLogger Log = new ILPostProcessorLogger();
    public override ILPostProcessor GetInstance() => this;

    MethodReference GetMethodRefResourcesLoad(AssemblyDefinition asmDef)
    {
        Type resourcesType = Type.GetType("UnityEngine.Resources, UnityEngine");

        MethodInfo loadMethodInfo = resourcesType
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "Load" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));


        return asmDef.MainModule.ImportReference(loadMethodInfo);
    }


    void Patch(MethodDefinition targetMethod, List<FieldDefinition> fieldsToPatch, MethodDefinition hook)
    {
        var ilProcessor = targetMethod.Body.GetILProcessor();

        var doesBodyEmpty = targetMethod.Body.Instructions.Count == 0;

        Instruction head = null;
        if (!doesBodyEmpty)
            head = targetMethod.Body.Instructions.First();
        foreach (var item in fieldsToPatch)
        {
            if (doesBodyEmpty)
            {
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
                ilProcessor.Append(ilProcessor.Create(OpCodes.Ldfld, item));
                ilProcessor.Append(ilProcessor.Create(OpCodes.Call, hook));
                ilProcessor.Append(ilProcessor.Create(OpCodes.Castclass, item.FieldType));
                ilProcessor.Append(ilProcessor.Create(OpCodes.Stfld, item));
            }
            else
            {
                ilProcessor.InsertAfter(head, ilProcessor.Create(OpCodes.Ldarg_0));                                         // Load 'this'
                ilProcessor.InsertAfter(head.Next, ilProcessor.Create(OpCodes.Ldarg_0));                                    // Load 'this' again for field load
                ilProcessor.InsertAfter(head.Next.Next, ilProcessor.Create(OpCodes.Ldfld, item));                           // Load Field
                ilProcessor.InsertAfter(head.Next.Next.Next, ilProcessor.Create(OpCodes.Call, hook));                       // Call Hook
                ilProcessor.InsertAfter(head.Next.Next.Next.Next, ilProcessor.Create(OpCodes.Castclass, item.FieldType));   // Cast Result
                ilProcessor.InsertAfter(head.Next.Next.Next.Next.Next, ilProcessor.Create(OpCodes.Stfld, item));            // Store in Foo field

                head = head.Next;
            }
        }
        ilProcessor.Append(ilProcessor.Create(OpCodes.Ret)); // Return
    }

    public List<FieldDefinition> GetFieldsInheritingFromCloudScriptableObject(TypeDefinition type)
    {
        var fields = new List<FieldDefinition>();

        foreach (var field in type.Fields)
        {
            if (field.FieldType.FullName == "Magix.CloudScriptableObject")
            {
                fields.Add(field);
            }
            else
            {
                try
                {
                    var fieldType = field.FieldType.Resolve();
                    while (fieldType != null)
                    {
                        if (fieldType.FullName == "Magix.CloudScriptableObject")
                        {
                            fields.Add(field);
                            break;
                        }

                        fieldType = fieldType.BaseType?.Resolve();
                    }
                }
                catch (AssemblyResolutionException)
                {
                    Log.LogDiagnostics("Failed to resolve type: " + field.FieldType.FullName);
                    Log.SaveLogsToFile("hello.txt");
                }
            }
        }

        return fields;
    }

    public static bool IsAssignableFromMonoBehaviour(TypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
            return false;

        const string monoBehaviourFullName = "UnityEngine.MonoBehaviour";

        var currentBaseType = typeDefinition.BaseType;
        while (currentBaseType != null)
        {
            if (currentBaseType.FullName == monoBehaviourFullName)
                return true;

            try
            {
                currentBaseType = currentBaseType.Resolve().BaseType;
            }
            catch (AssemblyResolutionException)
            {
                currentBaseType = null; // Can't resolve further, so stop the loop
            }
        }

        return false;
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
    {
        bool modified = false;
        using (var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData))
        {
            var assemblyResolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = assemblyResolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };



            using (var asmDef = AssemblyDefinition.ReadAssembly(peStream, readerParameters))
            {
                foreach (var type in asmDef.MainModule.Types)
                {
                    if (!IsAssignableFromMonoBehaviour(type))
                        continue;


                    Log.LogDiagnostics("HEAD");
                    Log.SaveLogsToFile("hello.txt");
                    var targetFields = GetFieldsInheritingFromCloudScriptableObject(type);

                    // Patch fields
                    //foreach (var field in targetFields)
                    //{

                    //    Log.LogDiagnostics("WE FOUND FIELD ITERATING....." + field.FullName);
                    //    Log.LogDiagnostics("EXIST: " + field.CustomAttributes?.Count());
                    //    Log.LogDiagnostics("DEMON: " + field.CustomAttributes.FirstOrDefault()?.AttributeType?.FullName);
                    //    Log.SaveLogsToFile("hello.txt");

                    //    if (field.CustomAttributes.Count() <= 0)
                    //    {
                    //        // Field is guaranteed is an scriptable object
                    //        var path = GetMethodReference01(asmDef).ToString()
                    //        field.CustomAttributes.Add(CreateAssetPathDescriptorAttribute(asmDef, path));
                    //    }
                    //}

                    if (targetFields?.Count <= 0)
                        continue;

                    var awakeMethod = type.Methods.FirstOrDefault(x => x.Name == "Awake");
                    var hookClass = asmDef.MainModule.Types.FirstOrDefault(t => t.FullName == "Magix.CloudScriptableObjectHook");
                    var loadRMethod = hookClass?.Methods.FirstOrDefault(m => m.Name == "LoadResource" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Object");
                    var loadRMethodRef = asmDef.MainModule.ImportReference(loadRMethod);

                    if (awakeMethod == null)
                    {
                        awakeMethod = new MethodDefinition("Awake", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig, asmDef.MainModule.TypeSystem.Void);
                        type.Methods.Add(awakeMethod);
                    }

                    Patch(awakeMethod, targetFields, loadRMethod);

                    modified = true;
                }

                Log.SaveLogsToFile("hello.txt");

                var pe = new MemoryStream();
                var pdb = new MemoryStream();

                var writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    SymbolStream = pdb,
                    WriteSymbols = true
                };
                asmDef.Write(pe, writerParameters);
                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), Log.Logs);
            }
        }

        Log.SaveLogsToFile("hello.txt");
        return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, Log.Logs);
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly)
    {
        const string TargetAssemblyName = "Assembly-CSharp";
        if (compiledAssembly.Name.Equals(TargetAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogDiagnostics($"Processing assembly: {compiledAssembly.Name}");
            Log.SaveLogsToFile("hello.txt");
            return true;
        }

        return false;
    }
}
