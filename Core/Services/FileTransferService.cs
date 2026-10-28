using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>Resultado de un envio. Rejected = el usuario del otro lado lo rechazo
/// a proposito: en ese caso no se reintenta por otro metodo.</summary>
public sealed record TransferOutcome(bool Success, string Method, string Message, bool Rejected = false);

/// <summary>
/// Envia ficheros a un dispositivo de la tailnet. Prioridad: LocalSend (si su app
/// responde en la IP de Tailscale del dispositivo); si no, Taildrop de Tailscale.
/// </summary>
public sealed class FileTransferService
{
    private readonly TailscaleService _tailscale = new();
    private readonly LocalSendService _localSend = new();

    public async Task<TransferOutcome> SendAsync(
        NetworkDevice device, IReadOnlyList<string> paths, IProgress<string> progress, CancellationToken ct)
    {
        progress.Report($"Buscando LocalSend en {device.Name}...");
        var baseUrl = await _localSend.ProbeAsync(device.Ip, ct);

        if (baseUrl != null)
        {
            var result = await _localSend.SendAsync(baseUrl, paths, progress, ct);

            // Un rechazo explicito no debe saltarse mandando el fichero por Taildrop.
            if (result.Success || result.Rejected) return result;

            progress.Report($"LocalSend ha fallado ({result.Message}). Probando con Tailscale...");
        }
        else
        {
            progress.Report("LocalSend no responde. Usando Tailscale...");
        }

        return await _tailscale.SendFilesAsync(device, paths, ct);
    }
}
