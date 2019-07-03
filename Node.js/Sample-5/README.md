# Rhino.Inside Chromium Embedded Framework (CEF)

This sample shows how to run Rhino.Inside the Chromium Embedded Framework. 
This sample has two parts:
1. `InsideCEF.WinForms`: A .NET .csproj which uses CEFSharp. Includes the code to initialize CEF inside a WinForms.Form window. Also includes code to start Rhino and Grasshopper, implemented in a custom `TaskScheduler`.
2. `InsideCEF.WebApp`: An html / javascript app using rhino3dm.js to deserialize geometry and three.js to visualize geometry.

