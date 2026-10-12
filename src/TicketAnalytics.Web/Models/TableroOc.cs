namespace TicketAnalytics.Web.Models;

public class ResumenOcPorTecnicoDto
{
    public string Tecnico { get; set; } = "";
    public int Total { get; set; }
    public int Vencidas { get; set; }
    public int VenceHoy { get; set; }
    public int VencenEsteMes { get; set; }
    public int VencenPronto { get; set; }
    public int EnCurso { get; set; }
    public int Completadas { get; set; }
    public int SinFecha { get; set; }
    public int Abiertas => Total - Completadas;
}

public class TableroOcDto
{
    public int Total { get; set; }
    public int Vencidas { get; set; }
    public int VenceHoy { get; set; }
    public int VencenEsteMes { get; set; }
    public int VencenPronto { get; set; }
    public int EnCurso { get; set; }
    public int Completadas { get; set; }
    public int SinFecha { get; set; }
    public List<ResumenOcPorTecnicoDto> PorTecnico { get; set; } = new();
    public List<OrdenCambioConTicketsDto> Ordenes { get; set; } = new();
}
