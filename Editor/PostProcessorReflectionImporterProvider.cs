using Mono.Cecil;

class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
{
    public IReflectionImporter GetReflectionImporter(ModuleDefinition moduleDefinition)
    {
        return new PostProcessorReflectionImporter(moduleDefinition);
    }
}


