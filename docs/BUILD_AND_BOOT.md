# Build reproduzível e prova de boot

> Atualização do passo 2: toolchain C++20 portátil e candidato fixado para avaliação Windows; provas restritas passaram até a entrada principal após InitHeap. O boot completo continua pendente. Consulte [as evidências e decisões atualizadas](NATIVE_BRINGUP.md). As decisões iniciais abaixo registram o ponto de partida.

## Disponível agora

Ambiente atual: Windows x64, PowerShell e compilador C# do .NET Framework em `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. O launcher tem como requisito .NET Framework 4.8. A inspeção não encontrou SDK .NET instalado, C++/CMake/Ninja no PATH nem CMake/Vulkan SDK nos caminhos padrão consultados; isso não exclui instalações em outros locais.

Executar `scripts/build.ps1` recompila diretamente os dois arquivos C# versionados; `scripts/test.ps1` compila e executa os testes sintéticos sem precisar do disco. Essa etapa não baixa dependências. `scripts/verify-profile.ps1` exige a cópia privada preparada e comprova apenas sua identidade técnica.

Esse processo é reproduzível funcionalmente no ambiente documentado. Ainda não há promessa de executáveis idênticos byte a byte; o compilador instalado não está fixado por distribuição e não há pipeline nativo implementado.

## Direção do build nativo

Alvo C++20/x64 com CMake; preferir MSVC e Windows SDK para o primeiro teste Windows. Confirmar a compatibilidade do backend antes de instalar componentes adicionais. O Vulkan SDK entra na etapa de renderer; sua versão e ferramentas de shader serão fixadas após o primeiro teste. Não instalar ferramentas globalmente apenas para preencher a arquitetura.

Antes de adotar uma dependência, criar um registro versionado com URL, commit exato, licença, subdependências e opções de build. `main`, `latest` e tags sem resolução para commit não servem como lock. A seleção do recompilador ainda está aberta.

O futuro `build-key` será o SHA-256 de um descritor JSON canônico contendo:

- Hash da imagem e do boot, versão e hash do perfil de geração.
- Hashes dos patches e dos mapas efetivamente consumidos.
- Commit do gerador, runtime e dependências transitivas.
- Versão do compilador, linker, Windows SDK, gerador CMake e opções.
- Backend gráfico e versões das ferramentas de shader quando usados.

Não incluir caminhos absolutos na chave. Ordenação, codificação UTF-8 e normalização serão especificadas na implementação; não aproveitar uma chave enquanto essa regra não existir. Saves não ficam dentro do cache de build.

## Primeira prova de boot: sequência de trabalho

| Ordem | Trabalho | Evidência de conclusão |
| --- | --- | --- |
| 1 | Verificar cópia e ferramentas atuais | Verificador e testes passam; nenhum dado do jogo no Git |
| 2 | Inventariar e validar toolchain C++ | Compilação/execução de um programa mínimo, versões registradas |
| 3 | Avaliar backend candidato | Fonte e licença revisadas, commit fixo, build e testes sem ROM no Windows |
| 4 | Mapear entrada e código alcançável | Relatório local com código/dados e lacunas explícitas |
| 5 | Gerar um conjunto controlado de funções | Relatório de instruções e chamadas sem traduções silenciosamente omitidas |
| 6 | Integrar caminho de boot ao runtime | Logs de CPU, BIOS, CD e dispositivos suficientes para localizar travamentos |
| 7 | Comparar com referência | Tela real reconhecível, entrada funcional e temporização documentada |

Se um backend não compilar, perder semântica de instruções ou não lidar com os carregamentos necessários, registrar o motivo e reavaliar a escolha antes de acoplar o launcher a ele. Boot não equivale a gameplay completo. O renderer Vulkan e a meta de 160 FPS têm critérios próprios posteriores.

## Dados da sessão e resultados

Logs futuros incluem identificação do build e do conteúdo, etapa e primeiro erro útil. Conteúdo extraído, fontes geradas e traces com memória ficam em `local/`. Relatórios públicos devem retirar caminhos pessoais. Memcards, estados e backups sobrevivem à limpeza dos caches e à atualização do runtime.
