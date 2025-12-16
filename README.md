# AutoReadonlyFields Source Generator

A C# Incremental Source Generator that automatically creates `private readonly` fields from C# 12 Primary Constructor parameters.

## 🚀 The Problem
C# 12 introduced **Primary Constructors**, which are great for dependency injection. However, if you want to store those parameters as private fields to use them across your class methods, you still have to manually declare the fields and assign them:

```csharp
// ❌ The old/manual way
public class UserService(ILogger logger, IRepository repo)
{
    // You have to manually type this boilerplate
    private readonly ILogger _logger = logger;
    private readonly IRepository _repo = repo;

    public void DoWork() => _logger.Log("Working...");
}
```

## ✨ The Solution
With this generator, simply mark your parameters with `[Readonly]` and the fields are generated for you automatically.

```csharp
// ✅ The new way
public partial class UserService(
    [Readonly] ILogger logger, 
    [Readonly] IRepository repo)
{
    // Fields _logger and _repo are auto-generated!
    public void DoWork() => _logger.Log("Working...");
}
```

## 📦 Requirements
*   **.NET SDK:** 6.0 or higher
*   **Language Version:** C# 12 or higher (required for Primary Constructors)

## 🛠 Installation

### Option 1: Project Reference (Local Development)
If you are including the generator source code directly in your solution:

1.  Add the `PrimaryConstructor.Readonly` project to your solution.
2.  In your consuming project's `.csproj` (e.g., `MyApp.csproj`), add the reference with specific attributes:

```xml
<ItemGroup>
  <ProjectReference Include="..\PrimaryConstructor.Readonly\PrimaryConstructor.Readonly" 
                    OutputItemType="Analyzer"/>
</ItemGroup>
```

### Option 2: NuGet (If you package it)
*(Skip this if you are just running it locally)*
```xml
<PackageReference Include="PrimaryConstructor.Readonly" Version="1.0.0" PrivateAssets="all" />
```

## 💻 Usage

### 1. Create a Partial Class
Create a class using a Primary Constructor. **You must add the `partial` keyword.**

```csharp
public partial class MyService([Readonly] HttpClient client)
{
    public async Task GetData()
    {
        // usage of _client is valid here because the generator created it
        var response = await _client.GetAsync("https://example.com");
    }
}
```

### 3. Build Project
The generator runs during the build process. Once built, you will see the generated files under your Dependencies/Analyzers node in Visual Studio.

## ⚙️ How it Works
The generator:
1.  Scans for classes marked `partial` with a Primary Constructor.
2.  Looks for parameters decorated with `[Readonly]`.
3.  Generates a separate partial class file containing:
    ```csharp
    private readonly Type _paramName = paramName;
    ```
4.  It handles Generic types (`MyClass<T>`) and Namespaces automatically.
