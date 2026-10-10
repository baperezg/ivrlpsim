using UnityEngine;

// Etiquetas del mapa de tejidos (una por vóxel).
// El orden numérico también es la prioridad al voxelizar: si dos estructuras
// se superponen, gana la de número mayor.
public enum Tissue : byte
{
    Air = 0,
    SoftTissue = 1,      // interior del cuerpo sin segmentar
    Fat = 2,
    Skin = 3,
    Muscle = 4,
    DuralSac = 5,         // malla "spinal cord": canal/saco dural (objetivo)
    EpiduralSpace = 6,
    OtherLigaments = 7,   // malla "Ligaments" (interespinoso, etc.)
    SupraspinousLigament = 8,
    LigamentumFlavum = 9,
    IntervertebralDisc = 10,
    Bone = 11,
}

public static class Tissues
{
    public const int Cantidad = 12;

    public static readonly string[] Nombres =
    {
        "Air", "SoftTissue", "Fat", "Skin", "Muscle", "Dural Sac", "Epidural Space",
        "Other Ligaments", "Supraspinous Ligament", "Ligamentum Flavum", "Disc", "Bone",
    };

    public static readonly Color32[] Colores =
    {
        new Color32(0, 0, 0, 0),        new Color32(200, 160, 150, 255), new Color32(242, 217, 102, 255),
        new Color32(242, 191, 166, 255), new Color32(191, 51, 51, 255),  new Color32(255, 224, 115, 255),
        new Color32(115, 191, 255, 255), new Color32(204, 204, 178, 255), new Color32(217, 178, 115, 255),
        new Color32(250, 209, 26, 255),  new Color32(115, 166, 230, 255), new Color32(240, 232, 214, 255),
    };

    // Nombre de la malla del gltf de Slicer -> tejido
    public static Tissue     DeMalla(string malla)
    {
        switch (malla)
        {            
            case "SkinThin": return Tissue.Skin;
            case "fat": return Tissue.Fat;
            case "right deep back muscle":
            case "left deep back muscle": return Tissue.Muscle;
            case "spinal cord": return Tissue.DuralSac;
            case "EpiduralSpace": return Tissue.EpiduralSpace;
            case "Ligaments": return Tissue.OtherLigaments;
            case "SupraspinousLigament": return Tissue.SupraspinousLigament;
            case "YellowLigament": return Tissue.LigamentumFlavum;
            case "intervertebral_discs": return Tissue.IntervertebralDisc;
            default: return Tissue.Bone; // vértebras, sacro, caderas
        }
    }
}
