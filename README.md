# Simulador_dotT (WPF)

Simulador de robô seguidor de linha com controle PID, desenvolvido em **.NET + WPF**.

O projeto permite ajustar parâmetros de controle em tempo real, acompanhar telemetria, medir voltas e aplicar sugestões automáticas de tuning.

## Funcionalidades

- Modos de operação: `Standby`, `MotorTestLeft`, `MotorTestRight`, `Calibration`, `FreeRace`, `Test3Laps`, `Official`
- Ajuste em tempo real de:
  - `KP`
  - `KI`
  - `KD`
  - `KSLIP`
  - velocidade base
- Renderização da pista, linha de largada, robô e sensores
- Telemetria em tempo real (erro e correção PID)
- Gerenciamento de voltas (tempo atual, melhor volta, histórico)
- Sugestões automáticas de ajuste PID com aplicação direta na interface

## Requisitos

- Windows
- .NET SDK com suporte a `net10.0-windows`

## Como executar

No diretório do projeto:

```powershell
dotnet restore
dotnet build .\Simulador_dotT.csproj
dotnet run --project Simulador_dotT.csproj
```

Se preferir executar o binário compilado:

```powershell
.\bin\Debug\net10.0-windows\Simulador_dotT.exe
```

## Estrutura do projeto

- `MainWindow.xaml` / `MainWindow.xaml.cs`: interface WPF e interação do usuário
- `Core/SimulationEngine.cs`: motor principal da simulação
- `Controllers/PIDController.cs`: lógica do controlador PID
- `Models/`
  - `Robot.cs`: cinemática e sensores do robô
  - `Track.cs`: geração da pista e progresso
  - `LapManager.cs`: detecção e gestão de voltas
- `Utils/`
  - `GraficoDataCollector.cs`: coleta de telemetria
  - `PIDDiagnostico.cs`: geração de sugestões de ajuste
  - `RankingStorage.cs`: persistência de resultados

## Fluxo básico de uso

1. Selecione o modo em **Operational Mode**.
2. Clique em **Start** para iniciar.
3. Ajuste `KP/KI/KD/KSLIP` e velocidade base conforme o comportamento.
4. Acompanhe status, progresso, tempos de volta e sugestões.
5. Use **Aplicar Sugestões** para tuning assistido.

## Observações

- A área da pista está configurada com fundo branco para melhor contraste visual.
- O build deve estar sem erros para o WPF carregar corretamente os handlers do XAML.
