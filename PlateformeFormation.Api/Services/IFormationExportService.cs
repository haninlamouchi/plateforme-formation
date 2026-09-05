using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IFormationExportService
{
    byte[] GeneratePdf(Formation formation);
}
