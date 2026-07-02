# pemdas

An ASP.NET Core MVC website that solves arithmetic expressions according to PEMDAS and explains each step.

## Features

- Accepts one expression containing up to six floating point numbers
- Supports grouping with `()`, `[]`, and `{}`
- Supports `+`, `-`, `*`, `/`, and `^`
- Shows each simplification step and a PEMDAS explanation
- Finishes with the final answer in simplified numeric form

## Run locally

```bash
dotnet run --project /home/runner/work/pemdas/pemdas/pemdas/pemdas.csproj
```

Then open the local URL printed by ASP.NET Core.

## Run tests

```bash
dotnet test /home/runner/work/pemdas/pemdas/pemdas.Tests/pemdas.Tests.csproj --nologo
```