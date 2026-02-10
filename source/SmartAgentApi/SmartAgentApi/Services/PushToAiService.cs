using AIAgent.Models;
using AIAgent.Services;
using CTMS.DataModel.Models.AIAgent;
using CTMS.DataModel.Models.ClinicalInformation;
using CTMS.Share.Helpers;
using Microsoft.Extensions.Options;
using SmartAgentApi.Models;

namespace SmartAgentApi.Services;

public class PushToAiService
{
    private readonly ILogger<PushToAiService> logger;
    private readonly AgentService agentService;
    private readonly Agentsetting agentsetting;

    public PushToAiService(ILogger<PushToAiService> logger,
        AgentService agentService,
        IOptions<Agentsetting> agentsettingOptions)
    {
        this.logger = logger;
        this.agentService = agentService;
        this.agentsetting = agentsettingOptions.Value;
    }

    public async Task<PatientAIInfo> Push(PatientDataModel patientData, string dicomImage)
    {
        PatientAIInfo patientAIInfo = new()
        {
            Age = patientData.Age,
            Code = patientData.Code,
            Gender = patientData.Gender,
            Height = patientData.Height,
            Weight = patientData.Weight,
            DicomFilename = dicomImage,
            DestionatioDicomFilename = "",
            DestionatioPatientJSONFilename = ""
        };
        patientAIInfo.InitKeyName();
        var currentRootPath = Directory.GetCurrentDirectory();
        var dicmTempRootPath = Path.Combine(currentRootPath, MagicObjectHelper.UploadTempPath);
        var sourceDicomFilePath = Path.GetDirectoryName(dicomImage);
        var destinationDicomFileName = Path.Combine(dicmTempRootPath, $"{patientAIInfo.KeyName}.dcm");
        File.Copy(dicomImage, destinationDicomFileName, true);

        agentService.CreateInBound(patientAIInfo, agentsetting);

        return patientAIInfo;
    }
}
