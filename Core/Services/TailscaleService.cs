using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Habla con el cliente local de Tailscale (tailscale.exe), sin credenciales ni API
/// de la nube: "status --json" para listar dispositivos y "file cp" (Taildrop) para
/// enviar ficheros. La gestion de reglas por API llegara en una fase posterior.
/// </summary>
public sealed class TailscaleService
{
    public static string? FindExecutable()
    {
        var folders = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tailscale")
        };
        folders.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var folder in folders)
        {
            try
            {
                var candidate = Path.Combine(folder, "tailscale.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // entrada de PATH invalida: se ignora
            }
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string exe, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar tailscale.exe");

        // Si se cancela, matamos el proceso para que no quede enviando en segundo plano.
        using var registration = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ya habia terminado */ }
        });

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await output, await error);
    }

    public async Task<(List<NetworkDevice> Devices, string? Error)> GetDevicesAsync(CancellationToken ct = default)
    {
        var devices = new List<NetworkDevice>();

        var exe = FindExecutable();
        if (exe == null) return (devices, "No se ha encontrado Tailscale. Instalalo e inicia sesion.");

        try
        {
            var (code, output, error) = await RunAsync(exe, new[] { "status", "--json" }, ct);
            if (string.IsNullOrWhiteSpace(output))
                return (devices, $"Tailscale no responde{(code != 0 ? $": {error.Trim()}" : "")}");

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            var state = root.TryGetProperty("BackendState", out var s) ? s.GetString() : null;
            if (!string.IsNullOrEmpty(state) && state != "Running")
                return (devices, $"Tailscale no esta conectado (estado: {state}).");

            if (root.TryGetProperty("Self", out var self)) devices.Add(ParseDevice(self, isSelf: true));

            if (root.TryGetProperty("Peer", out var peers) && peers.ValueKind == JsonValueKind.Object)
            {
                foreach (var peer in peers.EnumerateObject())
                    devices.Add(ParseDevice(peer.Value, isSelf: false));
            }

            devices = devices
                .OrderByDescending(d => d.IsSelf)
                .ThenByDescending(d => d.IsOnline)
                .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return (devices, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al leer el estado de Tailscale", ex);
            return (new List<NetworkDevice>(), ex.Message);
        }
    }

    private static NetworkDevice ParseDevice(JsonElement e, bool isSelf)
    {
        string Str(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

        var ip = "";
        if (e.TryGetProperty("TailscaleIPs", out var ips) && ips.ValueKind == JsonValueKind.Array)
        {
            ip = ips.EnumerateArray().Select(x => x.GetString() ?? "").FirstOrDefault(x => x.Length > 0 && !x.Contains(':')) ?? "";
        }

        DateTime? lastSeen = null;
        if (DateTime.TryParse(Str("LastSeen"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) &&
            parsed.Year > 2000)
        {
            lastSeen = parsed;
        }

        var online = isSelf || (e.TryGetProperty("Online", out var o) && o.ValueKind == JsonValueKind.True);
        var dns = Str("DNSName").TrimEnd('.');
        var host = Str("HostName");

        return new NetworkDevice
        {
            Name = string.IsNullOrWhiteSpace(host) ? dns : host,
            Target = string.IsNullOrWhiteSpace(dns) ? ip : dns,
            Ip = ip,
            Os = Str("OS"),
            IsSelf = isSelf,
            IsOnline = online,
            LastSeen = lastSeen
        };
    }

    /// <summary>Envia ficheros con Taildrop ("tailscale file cp"). Solo funciona hacia
    /// dispositivos del mismo usuario/tailnet y espera a que termine la transferencia.</summary>
    public async Task<TransferOutcome> SendFilesAsync(NetworkDevice device, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var exe = FindExecutable();
        if (exe == null) return new TransferOutcome(false, "Tailscale", "No se ha encontrado Tailscale.");

        var args = new List<string> { "file", "cp" };
        args.AddRange(paths);
        args.Add(device.Target + ":");

        try
        {
            var (code, _, error) = await RunAsync(exe, args, ct);
            return code == 0
                ? new TransferOutcome(true, "Tailscale", $"{paths.Count} fichero(s) enviados a {device.Name}.")
                : new TransferOutcome(false, "Tailscale", string.IsNullOrWhiteSpace(error) ? $"codigo de salida {code}" : error.Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo al enviar con Taildrop", ex);
            return new TransferOutcome(false, "Tailscale", ex.Message);
        }
    }
}
