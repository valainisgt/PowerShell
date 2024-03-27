using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Xunit;

namespace PSTests.Sequential
{
    public class RunspaceLeakTests : IDisposable
    {
        [Fact]
        public async Task RunspaceDoesNotLeakPSModulePath()
        {
            var initialPsModulePath = Environment.GetEnvironmentVariable("PSModulePath");

            this.runspace1.Open();
            using (var pwsh = PowerShell.Create(this.runspace1))
            {
                pwsh.AddScript("1 + 1");
                await pwsh.InvokeAsync();
            }
            var newPsModulePath1 = Environment.GetEnvironmentVariable("PSModulePath");

            this.runspace2.Open();
            using (var pwsh = PowerShell.Create(this.runspace2))
            {
                pwsh.AddScript("1 + 1");
                await pwsh.InvokeAsync();
            }
            var newPsModulePath2 = Environment.GetEnvironmentVariable("PSModulePath");

            var pwshPsModulePath = string.Empty;
            using (var pwsh = PowerShell.Create(this.runspace1))
            {
                pwsh.AddScript("return $env:PSModulePath");
                var results = await pwsh.InvokeAsync();
                pwshPsModulePath = results.First()?.ToString();
            }

            Assert.Equal(initialPsModulePath, newPsModulePath1);
            Assert.Equal(initialPsModulePath, newPsModulePath2);
            Assert.Equal(
                this.initialSessionState1.EnvironmentVariables.First(x => x.Name == "PSModulePath").Value,
                pwshPsModulePath
            );
        }

        private readonly DirectoryInfo path1;

        private readonly DirectoryInfo path2;

        private readonly InitialSessionState initialSessionState1;

        private readonly Runspace runspace1;

        private readonly InitialSessionState initialSessionState2;

        private readonly Runspace runspace2;

        public RunspaceLeakTests()
        {
            this.path1 = Directory.CreateDirectory(Guid.NewGuid().ToString("N"));
            this.path2 = Directory.CreateDirectory(Guid.NewGuid().ToString("N"));

            this.initialSessionState1 = InitialSessionState.CreateDefault2();
            this.initialSessionState1.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
            this.initialSessionState1.Variables.Remove("ErrorActionPreference", null);
            this.initialSessionState1.Variables.Add(
                new SessionStateVariableEntry(
                    "ErrorActionPreference",
                    "Stop",
                    null
                )
            );
            this.initialSessionState1.EnvironmentVariables.Add(new SessionStateVariableEntry(
                "PSModulePath",
                string.Join(
                    Path.PathSeparator,
                    new List<string>()
                    {
                        this.path1.FullName,
                        this.path2.FullName
                    }
                ),
                null
            ));

            this.runspace1 = RunspaceFactory.CreateRunspace(this.initialSessionState1);

            this.initialSessionState2 = InitialSessionState.CreateDefault2();
            this.initialSessionState2.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
            this.initialSessionState2.Variables.Remove("ErrorActionPreference", null);
            this.initialSessionState2.Variables.Add(
                new SessionStateVariableEntry(
                    "ErrorActionPreference",
                    "Stop",
                    null
                )
            );
            this.initialSessionState2.EnvironmentVariables.Add(new SessionStateVariableEntry(
                "PSModulePath",
                string.Join(
                    Path.PathSeparator,
                    new List<string>()
                ),
                null
            ));

            this.runspace2 = RunspaceFactory.CreateRunspace(this.initialSessionState2);
        }

        public void Dispose()
        {
            this.runspace2.Dispose();
            this.runspace2.Dispose();
            Directory.Delete(this.path1.FullName);
            Directory.Delete(this.path2.FullName);

            GC.SuppressFinalize(this);
        }
    }
}
