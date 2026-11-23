using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Makes every WebGL build playable:
///  - on any web host / itch.io (Gzip + decompression fallback, no server headers needed)
///  - by double-clicking index.html (file://), by embedding the build files as scripts and
///    serving them to Unity's loader through a fetch() shim.
/// </summary>
public class WebGLBuildFixes : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    const string Marker = "<!-- webgl-offline-shim -->";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        Debug.Log("[WebGLBuildFixes] Using Gzip compression with decompression fallback.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;

        string root = report.summary.outputPath;
        string buildDir = Path.Combine(root, "Build");
        string indexPath = Path.Combine(root, "index.html");
        if (!Directory.Exists(buildDir) || !File.Exists(indexPath))
        {
            Debug.LogWarning("[WebGLBuildFixes] Build folder or index.html not found, skipping offline support.");
            return;
        }

        string offlineDir = Path.Combine(buildDir, "offline");
        if (Directory.Exists(offlineDir)) Directory.Delete(offlineDir, true);
        Directory.CreateDirectory(offlineDir);

        // Everything Unity's loader fetches (data, framework, wasm). The loader itself loads via <script>, which works on file://.
        var files = Directory.GetFiles(buildDir)
            .Select(Path.GetFileName)
            .Where(f => !f.EndsWith(".loader.js", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var f in files)
        {
            string b64 = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(buildDir, f)));
            File.WriteAllText(Path.Combine(offlineDir, f + ".js"),
                "(window.__UNITY_OFFLINE__=window.__UNITY_OFFLINE__||{})['" + f + "']='" + b64 + "';\n",
                new UTF8Encoding(false));
        }

        string html = File.ReadAllText(indexPath);
        if (!html.Contains(Marker))
        {
            string list = string.Join(",", files.Select(f => "'" + f + "'"));
            string shim = Marker + "\n<script>\n" + ShimJs.Replace("__FILES__", list) + "\n</script>\n";
            int head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            html = head >= 0 ? html.Insert(head, shim) : shim + html;
            File.WriteAllText(indexPath, html, new UTF8Encoding(false));
        }

        Debug.Log("[WebGLBuildFixes] Added file:// support (" + files.Length + " embedded files).");
    }

    const string ShimJs = @"(function () {
  if (location.protocol !== 'file:') return;
  var files = [__FILES__];
  var store = window.__UNITY_OFFLINE__ = window.__UNITY_OFFLINE__ || {};
  var cache = {};
  for (var i = 0; i < files.length; i++)
    document.write('<script src=""Build/offline/' + files[i] + '.js""><\/script>');
  function toBytes(b64) {
    var bin = atob(b64), n = bin.length, u = new Uint8Array(n);
    for (var i = 0; i < n; i++) u[i] = bin.charCodeAt(i);
    return u;
  }
  var realFetch = window.fetch.bind(window);
  window.fetch = function (input, init) {
    var url = typeof input === 'string' ? input : (input && input.url) || '';
    var path = String(url).split('?')[0].split('#')[0];
    for (var i = 0; i < files.length; i++) {
      var f = files[i];
      if (path === f || path.slice(-(f.length + 1)) === '/' + f) {
        var bytes = cache[f];
        if (!bytes && store[f]) { bytes = cache[f] = toBytes(store[f]); store[f] = null; }
        if (bytes) {
          var type = /\.wasm/.test(f) ? 'application/wasm' : 'application/octet-stream';
          return Promise.resolve(new Response(bytes, { status: 200, headers: { 'Content-Type': type, 'Content-Length': String(bytes.length) } }));
        }
      }
    }
    return realFetch(input, init);
  };
})();";
}
