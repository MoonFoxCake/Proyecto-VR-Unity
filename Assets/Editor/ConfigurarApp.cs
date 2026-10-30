using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Menu: Tools > VR > Configurar nombre de la app (Quest)
/// Cambia el nombre del producto y el Package Name de Android, para que la app
/// no pise a otras apps del laboratorio al instalarla en el Quest.
/// Edita las tres constantes de abajo si quieren otros nombres.
/// </summary>
public static class ConfigurarApp
{
    const string NombreProducto = "Proyecto VR Casa";
    const string NombreEmpresa = "TEC IC-8073";
    // Solo letras, numeros y guion bajo; cada parte empieza con letra. Unico para su equipo.
    const string PackageAndroid = "com.tec.ic8073.casavr";

    [MenuItem("Tools/VR/Configurar nombre de la app (Quest)")]
    static void Configurar()
    {
        string antes =
            "Nombre: " + PlayerSettings.productName + "\n" +
            "Empresa: " + PlayerSettings.companyName + "\n" +
            "Package Android: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string despues =
            "Nombre: " + NombreProducto + "\n" +
            "Empresa: " + NombreEmpresa + "\n" +
            "Package Android: " + PackageAndroid;

        if (!EditorUtility.DisplayDialog("Configurar app",
                "ANTES\n" + antes + "\n\nDESPUES\n" + despues + "\n\nAplicar?", "Aplicar", "Cancelar"))
            return;

        PlayerSettings.productName = NombreProducto;
        PlayerSettings.companyName = NombreEmpresa;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageAndroid);
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("[App] Nombre y package configurados. Edit > Project Settings > Player para verificarlo.");
    }
}
