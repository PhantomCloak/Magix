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

    private bool IsInit = false;

    private MethodDefinition HookBehaviourLoadFunc = null;
    private MethodDefinition HookDirectLoadFunc = null;
    private MethodDefinition HookDirectAllLoadFunc = null;

    private const string FrameworkNamespace = "Magix";
	private const string HookNameResourceLoad = "LoadResourceDirect";
	private const string HookNameResourceLoadAll = "LoadResourceAllDirect";
	private const string LogFileName = "magix_diagnostics.txt";

    public override ILPostProcessor GetInstance() => this;

    void InitHookFuncs(AssemblyDefinition asmDef)
    {
        var hookClass = asmDef.MainModule.Types.FirstOrDefault(t => t.FullName == $"{FrameworkNamespace}.CloudScriptableObjectHook");

        HookBehaviourLoadFunc = hookClass?.Methods.FirstOrDefault(m => m.Name == "LoadResource" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Object");
        HookDirectLoadFunc = hookClass?.Methods.FirstOrDefault(m => m.Name == HookNameResourceLoad && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
        HookDirectAllLoadFunc = hookClass?.Methods.FirstOrDefault(m => m.Name == HookNameResourceLoadAll && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");

        if (HookBehaviourLoadFunc == null)
        {
            Log.LogDiagnostics("LoadResourceDirect Hook couldn't find");
        }

        if (HookDirectLoadFunc == null)
        {
            Log.LogDiagnostics("LoadResourceDirect Hook couldn't find");
        }

        if (HookDirectAllLoadFunc == null)
        {
            Log.LogDiagnostics("LoadResourceAllDirect Hook couldn't find");
        }

		Log.LogDiagnostics(LogFileName);

        IsInit = true;
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
            if (field.FieldType.FullName == $"{FrameworkNamespace}.CloudScriptableObject")
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
                        if (fieldType.FullName == $"{FrameworkNamespace}.CloudScriptableObject")
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
                    Log.SaveLogsToFile(LogFileName);
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

    private void PassResouceLoadHook(TypeDefinition typeDef, AssemblyDefinition asmDef)
    {
        foreach (var method in typeDef.Methods)
        {
            if (!method.HasBody)
                continue;

            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];

                if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference operand)
                {
                    continue;
                }

                if (operand.DeclaringType.FullName != "UnityEngine.Resources" || operand is not GenericInstanceMethod genericMethod || operand.Parameters.Count != 1)
                {
                    continue;
                }

                if (method.DeclaringType.FullName.StartsWith(FrameworkNamespace))
                    continue;

                var ilProcessor = method.Body.GetILProcessor();


                if (operand.Name == "Load")
                {
                    var genericHookMethod = new GenericInstanceMethod(HookDirectLoadFunc);

                    foreach (var arg in genericMethod.GenericArguments)
                    {
                        genericHookMethod.GenericArguments.Add(arg);
                    }

                    ilProcessor.Replace(instruction, ilProcessor.Create(OpCodes.Call, genericHookMethod));
                }
                else if (operand.Name == "LoadAll")
                {
                    var genericHookMethod = new GenericInstanceMethod(HookDirectAllLoadFunc);

                    foreach (var arg in genericMethod.GenericArguments)
                    {
                        genericHookMethod.GenericArguments.Add(arg);
                    }
                    ilProcessor.Replace(instruction, ilProcessor.Create(OpCodes.Call, genericHookMethod));
                }
            }
        }
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
    {
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
                if (!IsInit)
                    InitHookFuncs(asmDef);

                foreach (var type in asmDef.MainModule.Types)
                {
                    PassResouceLoadHook(type, asmDef);
                    if (!IsAssignableFromMonoBehaviour(type))
                        continue;


                    var targetFields = GetFieldsInheritingFromCloudScriptableObject(type);

                    if (targetFields?.Count <= 0)
                        continue;

                    var awakeMethod = type.Methods.FirstOrDefault(x => x.Name == "Awake");

                    if (awakeMethod == null)
                    {
                        awakeMethod = new MethodDefinition("Awake", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig, asmDef.MainModule.TypeSystem.Void);
                        type.Methods.Add(awakeMethod);
                    }

                    Patch(awakeMethod, targetFields, HookBehaviourLoadFunc);
                }


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
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly)
    {
        const string TargetAssemblyName = "Assembly-CSharp";
        if (compiledAssembly.Name.Equals(TargetAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
