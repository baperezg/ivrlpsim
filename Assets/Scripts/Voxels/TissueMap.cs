using System.IO;
using System.IO.Compression;
using UnityEngine;

// Cuadrícula de vóxeles con la etiqueta de tejido de cada punto, en coordenadas LPS (mm).
// El vóxel (i, j, k) tiene su centro en origen + (i, j, k) * espaciado.
// Es de solo lectura después de cargarse, así que se puede consultar desde el hilo háptico.
public class TissueMap
{
    public const string Archivo = "TissueMap.bytes";
    const uint Magico = 0x314A544D; // "MTJ1"

    public readonly int nx, ny, nz;
    public readonly Vector3 origen;
    public readonly float espaciado;
    public readonly byte[] etiquetas;
    readonly float inv;

    public TissueMap(int nx, int ny, int nz, Vector3 origen, float espaciado, byte[] etiquetas)
    {
        this.nx = nx; this.ny = ny; this.nz = nz;
        this.origen = origen; this.espaciado = espaciado; this.etiquetas = etiquetas;
        inv = 1f / espaciado;
    }

    public Tissue En(Vector3 lps)
    {
        int i = Mathf.RoundToInt((lps.x - origen.x) * inv);
        int j = Mathf.RoundToInt((lps.y - origen.y) * inv);
        int k = Mathf.RoundToInt((lps.z - origen.z) * inv);
        if ((uint)i >= nx || (uint)j >= ny || (uint)k >= nz) return Tissue.Air;
        return (Tissue)etiquetas[i + nx * (j + ny * k)];
    }

    public void Guardar(string ruta)
    {
        using (var f = File.Create(ruta))
        using (var gz = new GZipStream(f, System.IO.Compression.CompressionLevel.Optimal))
        using (var w = new BinaryWriter(gz))
        {
            w.Write(Magico);
            w.Write(nx); w.Write(ny); w.Write(nz);
            w.Write(origen.x); w.Write(origen.y); w.Write(origen.z);
            w.Write(espaciado);
            w.Write(etiquetas);
        }
    }

    public static TissueMap Cargar(string ruta)
    {
        using (var f = File.OpenRead(ruta))
        using (var gz = new GZipStream(f, CompressionMode.Decompress))
        using (var r = new BinaryReader(gz))
        {
            if (r.ReadUInt32() != Magico) throw new IOException("Archivo de mapa de tejidos inválido: " + ruta);
            int nx = r.ReadInt32(), ny = r.ReadInt32(), nz = r.ReadInt32();
            var o = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            float h = r.ReadSingle();
            var datos = new byte[nx * ny * nz];
            int leidos = 0;
            while (leidos < datos.Length)
            {
                int n = r.Read(datos, leidos, datos.Length - leidos);
                if (n <= 0) throw new IOException("Mapa de tejidos incompleto");
                leidos += n;
            }
            return new TissueMap(nx, ny, nz, o, h, datos);
        }
    }

    public static string RutaPorDefecto => Path.Combine(Application.streamingAssetsPath, Archivo);
}
