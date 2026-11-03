# Passo 2 — progresso da prova nativa

> Evolução posterior: o diagnóstico com mapas europeus já entra na rotina principal e encontrou timeout de VSync; veja [o estado do passo 3](STEP3_STATUS.md). Os resultados abaixo registram o marco anterior.

Estado: **em andamento; nenhuma tela do jogo foi validada**. A inicialização restrita foi traduzida, compilada e executada no Windows até uma parada explícita na entrada principal. Isso comprova uma faixa pequena da cadeia de recompilação, sem demonstrar gameplay.

## Ambiente efetivamente validado

Ferramentas portáteis em `local/tools`, sem alteração global de PATH ou instalação de SDKs:

| Ferramenta | Versão |
| --- | --- |
| w64devkit | 2.11.0, GCC 16.2.0 x64 |
| CMake | 4.4.5 |
| SDL2 para MinGW | 2.32.10 |
| PS1Recomp candidato | `ab4f0e01e6699c741bbc3d1ee73022be2803ff69` |

URLs e SHA-256 dos pacotes estão em `dependencies/native-tools.lock.json`; commit, submódulos, licenças principais e hash do patch estão em `dependencies/ps1recomp.lock.json`. A extração não executa o instalador SFX: usa `tar`. A verificação de hash se aplica aos arquivos de distribuição baixados; a presença do executável extraído não equivale a uma nova auditoria de integridade de toda a toolchain.

A direção inicial de preferir MSVC foi substituída, para esta avaliação, por GCC/MinGW portátil: compilou C++20 e permitiu testar o candidato sem instalar ferramentas globalmente. Isso não impede reavaliar MSVC para a versão de produto.

O backend continua **em avaliação**, com OpenGL apenas para diagnóstico. Não é uma adoção definitiva do runtime nem atende ainda ao requisito Vulkan 1.3.

## Reprodução

Na raiz do projeto, após preparar a cópia conhecida:

```powershell
.\scripts\build-candidate.ps1
.\scripts\test-native.ps1
.\scripts\analyze-game.ps1
.\scripts\probe-startup.ps1
.\scripts\probe-startup.ps1 -ThroughHeap
.\scripts\test.ps1
```

O primeiro comando baixa os pacotes fixados quando ausentes, confere os arquivos, faz checkout do commit esperado, inicializa os quatro submódulos de build e aplica o patch local. Um checkout existente em outro commit é preservado e gera erro. As dependências aninhadas de documentação/testes próprios do toml11 não são necessárias aos alvos usados aqui.

O build do candidato usa `PS1RECOMP_USE_STUB=ON`: o executável `ps1Runtime` resultante não contém o jogo. As provas separadas geram C++ privado e seus próprios executáveis em `local/analysis/<image-hash>/`. Não executar scripts de prova simultaneamente: eles compartilham os nomes dos artefatos e logs. O código MIPS original, as fontes geradas, os executáveis e os relatórios completos permanecem fora do Git.

MinGW Makefiles falhou com `Programação` no caminho longo; os scripts usam o nome Windows 8.3 do mesmo diretório. Se um ambiente não fornecer um nome ASCII, o script informa a limitação em vez de mover o projeto. Os logs ficam em `local/logs`.

## Adaptação Windows revisável

`patches/ps1recomp-windows.patch` contém mudanças no código upstream, sob a licença dele:

- Proteção das APIs POSIX de write guard e amostragem de stack; esses dois diagnósticos avisam que não estão disponíveis no host Windows.
- Backtrace Windows com `CaptureStackBackTrace`; os frames são endereços, sem promessa de simbolização automática.
- Includes SDL compatíveis com o pacote oficial, carregamento explícito das funções OpenGL modernas e tratamento de falha de carregamento.
- Caminhos curtos para a biblioteca importada SDL2, que o pacote resolvia novamente para o caminho Unicode.
- Stack de 16 MiB nos executáveis MinGW: objetos de memória do PS1 excederam a stack padrão Windows nos testes.
- Caminhos relativos para fixtures antes fixadas em `/tmp`; conversão explícita de `filesystem::path` para string; fechamento da imagem sintética antes de removê-la no Windows.
- Um teste dependente do comando POSIX `timeout` é explicitamente pulado no Windows. Não é reportado como aprovação.

O patch não inclui bytes do jogo nem alterações de instruções do dump. Não muda a política de segurança do Windows. Houve bloqueio local de execução por Controle de Aplicativo; após a alteração feita pelo proprietário do computador, o gerador executou. Nenhum script tenta contornar ou modificar essa política.

## Resultados dos testes sem ROM

| Suíte | Casos | Aprovados | Pulados | Falhas |
| --- | ---: | ---: | ---: | ---: |
| Analisador | 116 | 116 | 0 | 0 |
| Gerador | 108 | 107 | 1 | 0 |
| Runtime | 479 | 478 | 1 | 0 |
| Integração upstream | 4 | 1 | 3 | 0 |
| Total | 707 | 702 | 5 | 0 |

Os testes pulados dependem de fixtures externas de outros jogos ou do harness POSIX. Esses resultados não significam que a BIOS, os overlays ou o renderer de Digimon foram validados. Relatórios JSON e logs completos ficam em `local/logs`.

## Evidências sobre a cópia traduzida

O analisador produziu **938 funções candidatas** e **133 correspondências PsyQ**. Seu loader representa o payload inteiro de 921.600 bytes como `.text`; a indicação de zero bytes de dados não é um resultado confiável de separação código/dados.

Como auditoria, uma geração completa desse perfil heurístico produziu **38.042 marcadores de instrução inválida** e **2.576 stubs vazios gerados para JAL**. Essa saída foi rejeitada, não integrada ao jogo nem executada. `scripts/audit-generated.ps1 -SourcePath <arquivo.cpp>` identifica esses dois tipos de marcador e falha se encontrá-los. Passar nessa checagem simples também não prova correção do código.

Foram inventariados 318 arquivos `.PRO`, e o scanner retornou 312 candidatos a overlay. Os endereços estimados usam padrões de referências MIPS, não carregamentos observados em execução. Os arquivos incluem módulos potencialmente ligados a título, campo, batalha e minijogos; seus nomes não provam ordem ou endereço de carregamento. Não importar esses endereços como verdades no perfil final.

## Provas restritas da inicialização

Inspeção da entrada mostrou um limite menor que o sugerido pelo analisador: a rotina termina em `BREAK` em `0x80010F38`; a partir de `0x80010F3C` há uma tabela de stack. A prova traduz apenas 172 bytes, em vez dos 188 do limite heurístico.

| Evidência | Resultado |
| --- | --- |
| Entrada traduzida | `0x80010E90` |
| Faixa limpa pelo startup | `[0x8005CCE8, 0x80082CB0)` |
| Stack após inicialização | `0x80200000`, topo exclusivo da RAM |
| Global pointer | `0x8005CB50` |
| Endereço solicitado a InitHeap | `0x80082CB4` |
| Tamanho solicitado | `0x175350` |
| Wrapper de InitHeap | `0x8002507C`, 16 bytes inspecionados |
| Serviço usado pelo wrapper | BIOS A0:39, via HLE do candidato |
| Próxima entrada, com parada diagnóstica | `0x80014524` |

A prova envenena a região BSS antes de executar e verifica que ela foi limpa sem ultrapassar seus limites, exceto o primeiro word, onde a rotina guarda o RA do chamador. Também verifica stack, GP, argumento calculado no delay slot e, em `-ThroughHeap`, o cabeçalho de heap escrito pelo serviço BIOS. As chamadas de fronteira lançam uma exceção identificada; não retornam fingindo implementar o jogo. A rotina principal ainda não foi executada.

## BIOS fornecida

`bios/SCPH1001.BIN` foi observado com 524.288 bytes e está protegido pelo `.gitignore`. O nome não foi usado para afirmar autenticidade, região ou compatibilidade. O runtime candidato usa BIOS HLE neste caminho; o arquivo fornecido não foi carregado nem executado pelas provas. Caso um serviço exija comparação com BIOS real, isso terá configuração e validação explícitas.

## Trabalho restante para concluir o passo 2

1. Mapear a rotina de `0x80014524` e suas primeiras dependências, separando caminhos executados, dados e chamadas indiretas.
2. Identificar no loader real os endereços, tamanhos, relocação e ativação dos `.PRO`, especialmente o módulo de título.
3. Revisar correspondências PsyQ e serviços BIOS do caminho de boot; evitar substituições HLE baseadas apenas em nomes ou heurística.
4. Gerar um perfil que não dependa de instruções inválidas descartadas nem stubs vazios, com cobertura documentada.
5. Integrar esse perfil a um executável diagnóstico e comparar uma tela real do jogo e a entrada com referência.

Uma cópia sem modificações da mesma edição/região pode servir de comparação, mantendo a tradução como perfil próprio. Ela ajuda a atribuir diferenças; não foi imposta como requisito para executar as provas atuais.

## Fontes

- [PS1Recomp no commit avaliado](https://github.com/PS1Recomp/ps1-recomp/tree/ab4f0e01e6699c741bbc3d1ee73022be2803ff69): código, testes e licença foram inspecionados localmente.
- Os locks apontam para os pacotes oficiais de [w64devkit](https://github.com/skeeto/w64devkit), [CMake](https://github.com/Kitware/CMake) e [SDL](https://github.com/libsdl-org/SDL).
