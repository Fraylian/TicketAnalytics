namespace TicketAnalytics.Domain.Common;

/// <summary>
/// Analistas del grupo/área. Se usa para limitar los selectores de responsable
/// en TODA la aplicación (órdenes de cambio, matriz de proveedores, diario TI,
/// seguimiento, analistas) a las personas del equipo. Editar aquí si cambia el equipo.
/// Los userid permiten resolverlos de forma confiable contra ca_contact.
/// </summary>
public static class GrupoAnalistas
{
    /// <summary>userid en ca_contact de cada miembro del grupo.</summary>
    public static readonly string[] UserIds =
    {
        "jalvarado",  // Alvarado, Jhon Jairo
        "rceballos",  // Ceballos Marchena, Ransel
        "fsoto",      // Fraylian Alejandro, Soto Garcia
        "nimelo",     // Melo, Nimel
        "mpozo",      // Pozo Ferreras, Maximo
        "yasoto"      // Soto De Salazar, Yarinet A.
    };

    /// <summary>Nombres de respaldo (por si se compara por nombre y no por userid).</summary>
    public static readonly string[] Miembros =
    {
        "Jhon Jairo Alvarado",
        "Ransel Ceballos Marchena",
        "Fraylian Alejandro Soto Garcia",
        "Nimel Melo",
        "Maximo Pozo Ferreras",
        "Yarinet A. Soto De Salazar"
    };

    /// <summary>True si el nombre pertenece al grupo (comparación laxa por tokens del nombre).</summary>
    public static bool EsDelGrupo(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;
        var n = nombre.Trim();
        return Miembros.Any(m =>
            string.Equals(m, n, StringComparison.OrdinalIgnoreCase)
            || TokensCoinciden(m, n));
    }

    private static bool TokensCoinciden(string a, string b)
    {
        var ta = a.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var tb = b.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        // Coinciden si comparten al menos 2 tokens (nombre + apellido en cualquier orden)
        int comunes = ta.Count(x => tb.Any(y => string.Equals(x, y, StringComparison.OrdinalIgnoreCase)));
        return comunes >= 2;
    }
}
