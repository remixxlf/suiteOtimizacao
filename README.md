# ⚡ Suíte de Otimização All-in-One (CoreIsolator + Tweaks)

Esta aplicação é a unificação de dois projetos poderosos para otimização do Windows:
1. **CoreIsolator**: Um gerenciador avançado de afinidade de CPU (WPF / .NET 8) que isola núcleos do processador para jogos e aplicações de alta performance.
2. **Otimizador Windows**: Um conjunto de scripts avançados de PowerShell para limpeza profunda, desativação de telemetria nativa e tweaks no Registro.

Tudo isso envelopado em uma única interface moderna, rápida e assíncrona, conectada a um serviço de telemetria na nuvem para métricas de sucesso.

## 🚀 Funcionalidades

- **Gerenciamento de CPU**: Detecção de topologia (P-Cores e E-Cores em processadores Intel) e isolamento de processos em tempo real via P/Invoke.
- **Limpeza e Tweaks Integrados**: Execução segura e não-bloqueante de scripts PowerShell (como o `Otimizador_Windows.ps1`) com captura de logs em tempo real na interface gráfica.
- **Arquitetura Moderna**:
  - Construído em **C# / .NET 8 WPF**.
  - Padrão **MVVM** com `CommunityToolkit.Mvvm`.
  - **Injeção de Dependência** via `Microsoft.Extensions.Hosting`.
  - Processamento Assíncrono Avançado (`IAsyncEnumerable`, `System.Threading.Channels`).
- **Telemetria Serverless**: Cliente HTTP com `IHttpClientFactory` que se comunica com uma API Node.js (Vercel) usando chamadas *fire-and-forget*.

## 🛠️ Como Executar e Compilar

### Pré-requisitos
- [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (Recomendado) ou VS Code.

### Passos
1. Clone este repositório.
2. Abra a pasta do projeto e execute:
   ```bash
   dotnet build
   dotnet run
   ```
3. A aplicação será aberta. Você pode acessá-la pela Bandeja do Sistema (System Tray) ou diretamente pela janela principal.
4. Para acessar a nova suíte de limpeza, clique no botão **"🛠️ Otimizar PC"** na barra de título.

## 📁 Estrutura do Projeto

- `/Scripts`: Contém os artefatos em PowerShell `.ps1` que são copiados para a pasta de saída durante a compilação.
- `/Services`: Orquestradores assíncronos (`PowerShellRunnerService`, `TelemetryClient`).
- `/ViewModels`: Camada lógica da UI (`TweaksViewModel`).
- `/Views`: XAML das interfaces gráficas (ex: `TweaksWindow.xaml` com visual *Dark Theme Premium* e *Glassmorphism*).

---
*Projeto arquitetado e unificado visando máxima performance do sistema operacional.*
