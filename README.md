# Smart IA — MVP

> API .NET 10 para processamento de arquivos CSV/Excel com agregação de dados, recomendação automática de gráficos e geração de insights em linguagem natural via LLM.

---

## Índice

- [Visão Geral](#visão-geral)
- [Funcionalidades](#funcionalidades)
- [Arquitetura](#arquitetura)
- [Stack Tecnológica](#stack-tecnológica)
- [Pré-requisitos](#pré-requisitos)
- [Configuração](#configuração)
- [Como Executar](#como-executar)
- [Endpoints](#endpoints)
  - [POST /api/report/process](#1-gerar-relatório--post-apireportprocess)
  - [POST /api/chart/recommend](#2-recomendar-gráfico--post-apichartrecommend)
- [Segurança e Sanitização](#segurança-e-sanitização)
- [CORS / Frontend](#cors--frontend)
- [Testes](#testes)

---

## Visão Geral

O **Smart IA MVP** recebe arquivos `.csv`, `.xlsx` ou `.xls`, processa e agrega os dados, e usa um LLM (DeepSeek com endpoint compatível OpenAI, via Microsoft Semantic Kernel) para:

- Recomendar automaticamente o tipo de gráfico mais adequado para os dados
- Gerar insights em linguagem natural prontos para exibição

Quando o LLM não está disponível, a API cai em **fallback determinístico** e retorna insights baseados no líder do ranking.

---

## Funcionalidades

| Funcionalidade                 | Descrição                                                                                         |
| ------------------------------ | ------------------------------------------------------------------------------------------------- |
| Upload de arquivos             | Aceita `.csv`, `.xlsx` e `.xls`                                                                   |
| Agregação de dados             | Detecção automática de colunas X/Y, agrupamento Top N, cálculo de total, média, share Top 1/Top 3 |
| Recomendação de gráfico        | Retorna `chartType`, colunas sugeridas, título e justificativa                                    |
| Geração de blocos de relatório | `title`, `typeGraphic`, `labels`, `datasets`, `insightsIa`                                        |
| Suporte a múltiplas abas       | Excel gera 1 bloco por aba; CSV gera 1 bloco                                                      |
| Filtros opcionais              | Período e colunas via `ParametersJson`                                                            |
| Sanitização de dados           | Whitelist de colunas + anonimização de CPF/CNPJ e e-mail antes do envio ao LLM                    |
| Fallback sem IA                | Insight determinístico quando o LLM está indisponível                                             |

### Lógica de detecção de gráfico (`GraphicTypeDetector`)

| Condição              | Tipo retornado |
| --------------------- | -------------- |
| Labels são datas      | `line`         |
| ≤ 5 labels e soma > 0 | `pie`          |
| Demais casos          | `bar`          |

---

## Arquitetura

Clean Architecture em 4 projetos:

```
smart-ia-mvp/
├── src/
│   ├── SmartIa.Api/             # Controllers, DTOs de request, Program.cs, Swagger, CORS
│   ├── SmartIa.Application/     # UseCases, Interfaces, DTOs, Domain (GraphicTypeDetector, Policies)
│   ├── SmartIa.Infrastructure/  # AI (Semantic Kernel), Files (CSV/Excel), Sanitization, Reports
└── tests/
    └── SmartIa.Tests/           # xUnit
```

### Fluxo de processamento

```
HTTP Request
    │
    ▼
ReportController / ChartController
    │  (IFormFile)
    ▼
GenerateReportUseCase / RecommendChartUseCase
    │
    ├──► IDataProcessService
    │       ├── FileRecordsReader   (leitura CSV/Excel)
    │       ├── DatasetAnalyzer    (agregação, metadados)
    │       └── TextCleaning       (normalização)
    │
    ├──► IDataSanitizerService
    │       └── DataSanitizerService  (whitelist, anonimização)
    │
    └──► IAiInsightService / IChartAiService
             └── Kernel.InvokePromptAsync  (DeepSeek via Semantic Kernel)
```

---

## Stack Tecnológica

| Camada              | Tecnologia                                             |
| ------------------- | ------------------------------------------------------ |
| Runtime             | .NET 10 / ASP.NET Core Web API                         |
| LLM                 | Microsoft Semantic Kernel 1.80.1 + `Connectors.OpenAI` |
| Leitura de arquivos | CsvHelper 33.1.0, ExcelDataReader 3.9.0                |
| Documentação        | Swashbuckle.AspNetCore (Swagger)                       |
| Testes              | xUnit                                                  |

---

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Chave de API de um provedor LLM compatível com OpenAI (DeepSeek por padrão)

---

## Configuração

Edite `src/SmartIa.Api/appsettings.json`:

```json
{
  "LLM": {
    "Provedor": "DeepSeek",
    "ApiKey": "sk-...",
    "ModelId": "deepseek-chat",
    "Endpoint": "https://api.deepseek.com"
  }
}
```

> ⚠️ **Nunca commite a chave real.** Use User Secrets ou variáveis de ambiente em desenvolvimento:

```bash
dotnet user-secrets init --project src/SmartIa.Api
dotnet user-secrets set "LLM:ApiKey"      "sk-..."                    --project src/SmartIa.Api
dotnet user-secrets set "LLM:ModelId"     "deepseek-chat"             --project src/SmartIa.Api
dotnet user-secrets set "LLM:Endpoint"    "https://api.deepseek.com"  --project src/SmartIa.Api
```

> Se `ApiKey`, `ModelId` ou `Endpoint` estiverem vazios, o Kernel sobe sem provedor e os serviços de IA operam em modo fallback.

---

## Como Executar

```bash
dotnet restore
dotnet build
dotnet run --project src/SmartIa.Api
```

URLs disponíveis localmente (conforme `launchSettings.json`):

| Protocolo | URL                           |
| --------- | ----------------------------- |
| HTTP      | http://localhost:5259         |
| HTTPS     | https://localhost:7145        |
| Swagger   | http://localhost:5259/swagger |

---

## Endpoints

### 1. Gerar relatório — `POST /api/report/process`

Recebe um arquivo e filtros opcionais, retorna blocos prontos para renderização de gráficos.

**Content-Type:** `multipart/form-data`

| Campo            | Tipo        | Obrigatório | Descrição                    |
| ---------------- | ----------- | ----------- | ---------------------------- |
| `File`           | arquivo     | ✅          | `.csv`, `.xlsx` ou `.xls`    |
| `ParametersJson` | string JSON | ❌          | Filtros de período e colunas |

**Exemplo de `ParametersJson`:**

```json
{
  "dateIni": "2024-01-01T00:00:00",
  "dateEnd": "2024-12-31T00:00:00",
  "columnX": "Regiao",
  "columnY": "Total"
}
```

**Resposta `200 OK`:**

```json
{
  "blocks": [
    {
      "title": "Vendas",
      "typeGraphic": "bar",
      "labels": ["Sudeste", "Sul", "Nordeste"],
      "datasets": [{ "label": "Total", "data": [1500.5, 980.0, 720.25] }],
      "insightsIa": "Sudeste lidera com 43% do total..."
    }
  ]
}
```

**Erros possíveis:**

| Código | Mensagem                                                | Causa                    |
| ------ | ------------------------------------------------------- | ------------------------ |
| `400`  | `"O arquivo Csv é obrigatorio"`                         | Nenhum arquivo enviado   |
| `400`  | `"ParametersJson inválido"`                             | JSON malformado          |
| `400`  | `{"message":"Nenhum bloco do relatório foi gerado..."}` | Sem dados após agregação |

**Exemplo cURL:**

```bash
curl -X POST http://localhost:5259/api/report/process \
  -F "File=@./vendas.csv" \
  -F 'ParametersJson={"columnX":"Regiao","columnY":"Total"}'
```

---

### 2. Recomendar gráfico — `POST /api/chart/recommend`

Analisa o arquivo e retorna a recomendação de visualização mais adequada.

**Content-Type:** `multipart/form-data`

| Campo  | Tipo                      | Obrigatório |
| ------ | ------------------------- | ----------- |
| `File` | `.csv`, `.xlsx` ou `.xls` | ✅          |

**Resposta `200 OK`:**

```json
{
  "chartType": "bar",
  "columnX": "Regiao",
  "columnY": "Total",
  "title": "Total por Regiao",
  "reason": "Categoria com métrica agregável — gráfico de barras facilita comparação entre grupos."
}
```

**Exemplo cURL:**

```bash
curl -X POST http://localhost:5259/api/chart/recommend \
  -F "File=@./vendas.xlsx"
```

---

## Segurança e Sanitização

O `DataSanitizerService` aplica as seguintes proteções antes de qualquer dado chegar ao LLM:

- **Whitelist de colunas** — apenas colunas reconhecidas são enviadas ao prompt (ex.: `Empresa`, `Filial`, `Categoria`, `Produto`, `Regiao`, `UF`, `Cidade`, `Canal`, `Qtd`, `Total`, `Frete`, além de prefixos `Data`, `Dt `, `Emissao`, `Validade`). Demais colunas são descartadas.
- **Anonimização de PII** — valores contendo CPF, CNPJ ou e-mail são substituídos antes do envio.
- **Restrições no prompt** — os prompts proíbem inferência de moeda (`R$`, `$`, `%`) e limitam a resposta a 3 frases (líder, concentração, ação sugerida), com `Temperature=0` para máxima determinismo.

---

## CORS / Frontend

A política `AllowAngular` libera `http://localhost:4200` com qualquer método e header.

Para outros ambientes, ajuste a origem em `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("https://seu-dominio.com")
              .AllowAnyMethod()
              .AllowAnyHeader());
});
```

---

## Testes

```bash
dotnet test
```

Os testes estão em `tests/SmartIa.Tests/` usando xUnit.
