using Mythos.Framework;

if (FrameworkAssembly.Name != "Mythos.Framework")
{
    Console.Error.WriteLine("Framework assembly identity is invalid.");
    return 1;
}

Console.WriteLine("Mythos framework smoke test passed.");
return 0;
