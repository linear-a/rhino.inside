# Rhino.Inside.Javascript
### Running Rhino and Grasshopper inside various Javascript Engines (Node.js, Electron, Chromium Embedded Framework)

## Samples

1. [Sample 1](Sample-1): Rhino.Inside.Node 1 -
2. [Sample 2](Sample-2): Rhino.Inside.Node 2 -
3. [Sample 3](Sample-3): Rhino.Inside.Node 3 - 
4. [Sample 4](Sample-4): Rhino.Inside.Electron - This example launches an Electron window, calls methods from Javascript to .NET to launch Rhino, and launch Grasshopper. When meshes are added to the Grasshopper definition, these meshes will be rendered in the Chromium browser by three.js.
5. [Sample 5](Sample-5): Rhino.Inside.CEF (Chromium Embedded Framework via CefSharp) - This example embeds a Chromium Browser inside a WinForm window, launches Rhino, and launches Grasshopper. When meshes are added to the Grasshopper definition, these meshes will be rendered in the Chromium browser by three.js.

## Javascript Runtimes / Frameworks

The samples show how to run Rhino.Inside the following Javascript frameworks:
- [`Node.js`](https://nodejs.org/en/) - Sample 1, Sample 2, and Sample 3. Node.js® is a JavaScript runtime built on Chrome's V8 JavaScript engine.
- [`Electron.js`](https://electronjs.org/) - Sample 4. Electron is an open-source framework developed and maintained by GitHub. Electron allows for the development of desktop GUI applications using web technologies: It combines the Chromium rendering engine and the Node.js runtime.
- [`Chromium Embedded Framework (CefSharp)`](https://cefsharp.github.io/) - Sample 5. The Chromium Embedded Framework is an open-source software framework for embedding a Chromium web browser within another application. 

## Dependencies

- [`Edge.js`](https://github.com/agracio/edge-js) - This is used to call .NET functions from Javascript in Sample 1, Sample 2, Sample 3, and Sample 4. Sample 5 uses the mechanism in CEF to pass data between .NET and Javascript.

