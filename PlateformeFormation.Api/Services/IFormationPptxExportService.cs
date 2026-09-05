using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IFormationPptxExportService
{
    byte[] GeneratePptx(Formation formation);
}
