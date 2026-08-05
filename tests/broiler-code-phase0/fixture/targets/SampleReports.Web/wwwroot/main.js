import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);

document.getElementById('output').textContent =
    exports.SampleReports.Web.Program.Render();
