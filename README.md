# Simulador_dotT (WPF)

## Resumo

Este projeto apresenta um simulador de robô seguidor de linha com controle PID e modelagem de acionamento por PWM, implementado em .NET (WPF). O objetivo é reproduzir, em ambiente de simulação, aspectos de controle e dinâmica observados em plataformas reais: erro de rastreamento, atuação diferencial dos motores, limitação física de resposta e avaliação por voltas.

## Objetivo da Pesquisa

Investigar o comportamento de um seguidor de linha sob variações de parâmetros de controle e de acionamento, com foco em:

- estabilidade de rastreamento;
- tempo de volta;
- robustez frente a saturação e não-linearidades de motor;
- aproximação entre simulação e comportamento embarcado.

## Metodologia de Desenvolvimento

O desenvolvimento foi conduzido em camadas, de forma incremental:

1. **Modelagem geométrica da pista** com curva fechada e parâmetro contínuo de progresso $T \in [0,1]$.
2. **Modelagem cinemática do robô diferencial** (posição, orientação e sensores virtuais).
3. **Controle PID** com anti-windup e diagnóstico de oscilação.
4. **Gerenciamento de voltas** com detecção de cruzamento em linha de largada e interpolação temporal.
5. **Telemetria e análise** (erro, correção, velocidades e histórico de voltas).
6. **Camada PWM** para representar melhor a resposta de motores DC reais.
7. **Interface WPF** para operação, ajuste de parâmetros e visualização dos resultados em tempo real.

## Implementação Técnica

### Arquitetura de Módulos

- `MainWindow.xaml` / `MainWindow.xaml.cs`: interface e interação do usuário
- `Core/SimulationEngine.cs`: orquestração da simulação
- `Controllers/PIDController.cs`: cálculo de correção PID
- `Models/Robot.cs`: dinâmica diferencial e sensores
- `Models/Track.cs`: geração da pista e progressão
- `Models/LapManager.cs`: cronometragem e voltas
- `Utils/GraficoDataCollector.cs`: coleta de dados
- `Utils/PIDDiagnostico.cs`: sugestões automáticas de tuning
- `Utils/PWMSimulator.cs`: modelo PWM (duty cycle, dead zone, não-linearidade, inércia)

### Controle PID

O controlador utiliza a forma clássica:

$$
u(t) = K_P e(t) + K_I \int e(t)dt + K_D \frac{de(t)}{dt}
$$

com limitação da integral (anti-windup) e aplicação condicional de `KSLIP` para cenários de maior desvio.

### Camada PWM (Implementação Adicionada)

A integração PWM foi implementada para substituir o acoplamento direto entre saída de controle e velocidade de motor.

Fluxo de atuação:

1. `SimulationEngine` gera velocidades alvo de cada roda;
2. velocidades alvo são convertidas para duty cycle (`0-100%`);
3. `PWMSimulator` aplica:
   - **dead zone** (abaixo do limiar o motor não gira);
   - **curva não-linear** de conversão duty → velocidade;
   - **inércia/aceleração limitada** por ciclo de atualização;
4. velocidades efetivas são aplicadas ao `Robot`.

## O que Deve Acontecer (Comportamento Esperado)

Com base na implementação atual, o comportamento esperado do sistema é:

- rastrear a pista mantendo erro baixo em regimes estáveis;
- reduzir oscilações por amortecimento derivativo e limitação de atuação;
- exibir PWM esquerdo/direito em tempo real para análise de saturação;
- registrar voltas, melhor volta e evolução temporal de desempenho;
- permitir ajustes manuais e assistidos (`PIDDiagnostico`) para convergência de parâmetros.

## Resultados Obtidos no Desenvolvimento

Durante o desenvolvimento e validação desta versão, foram obtidos os seguintes resultados práticos:

- **migração concluída para WPF**, mantendo os principais recursos do simulador;
- **integração de PWM concluída** no pipeline de atuação dos motores;
- **indicadores visuais de PWM** adicionados na interface;
- **projeto compilando com sucesso** após integração (`dotnet build`);
- **publicação no GitHub concluída** com histórico sincronizado.

Observação: os resultados acima se referem ao estado de engenharia e integração funcional do projeto. Para relatório experimental quantitativo (ex.: médias de tempo por configuração PID), recomenda-se executar campanhas de teste controladas por modo de operação.

## Requisitos e Execução

### Requisitos

- Windows
- .NET SDK com suporte a `net10.0-windows`

### Execução

```powershell
dotnet restore
dotnet build .\Simulador_dotT.csproj
dotnet run --project Simulador_dotT.csproj
```

Binário compilado:

```powershell
.\bin\Debug\net10.0-windows\Simulador_dotT.exe
```

## Limitações e Trabalhos Futuros

- modelagem dinâmica ainda simplificada (sem atrito detalhado e carga variável);
- ausência de protocolo estatístico automatizado para comparação entre tunings;
- possibilidade de expansão para exportação de telemetria e análise offline;
- possibilidade de calibração automática de ganhos com busca heurística ou otimização.
