namespace TicketAnalytics.Web.Models;

public class BandejaAnalistasDto
{
    public int TotalAbiertos { get; set; }
    public int Vencidos { get; set; }
    public int VencenHoy { get; set; }
    public int VencenPronto { get; set; }
    public int EnCurso { get; set; }
    public int SinFecha { get; set; }
    public List<AnalistaBandejaDto> Analistas { get; set; } = new();
}

public class AnalistaBandejaDto
{
    public string Analista { get; set; } = "";
    public int TotalAbiertos { get; set; }
    public int Vencidos { get; set; }
    public int VencenHoy { get; set; }
    public int VencenPronto { get; set; }
    public int EnCurso { get; set; }
    public int SinFecha { get; set; }
    public List<TicketBandejaDto> Tickets { get; set; } = new();
}

public class TicketBandejaDto
{
    public string NoTicket { get; set; } = "";
    public string? Titulo { get; set; }
    public string? Categoria { get; set; }
    public string? Estado { get; set; }
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public int? DiasParaVencer { get; set; }
    public string EstadoVencimiento { get; set; } = "sin_fecha";
}
