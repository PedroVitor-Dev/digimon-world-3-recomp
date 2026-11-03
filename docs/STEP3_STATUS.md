# Passo 3 — estado e pré-requisitos

O objetivo permanece: menus, exploração, batalhas, transições, vídeo, áudio, controles e memory cards funcionando na temporização original, com uma sessão salva e retomada. **O passo 3 não está concluído**; o passo 2 ainda precisa alcançar e validar uma tela real do jogo.

## Avanço no boot

A referência MIT [ReGame-Labs/dw3_decomp](https://github.com/ReGame-Labs/dw3_decomp/tree/791932b757b358bb8228f851ce96f4d22ae112dd) foi fixada em `dependencies/dw3-decomp.lock.json`. São consumidos mapas e símbolos, não o C reconstruído do jogo. Arquivos de referência têm hashes fixos e permanecem no checkout privado.

Os mapas europeus coincidem com as âncoras observadas na tradução: entrada `0x80010E90`, GP `0x8005CB50`, BSS e InitHeap. Entretanto, o boot traduzido tem SHA-1 `ae75434288284f85ecf01103705c38998ba30984`, diferente do `d1b7e4d646e3a9c2b88fdb25d20b5f7116bbb06d` informado pela referência europeia. Essa coincidência parcial não prova equivalência de todos os bytes ou compatibilidade dos módulos.

O mapa separa o código residente (`0x80010E88` até `0x8003EDCC`, fim exclusivo) dos dados e do TIM no restante da imagem. O builder inclui 913 entradas/aliases candidatos, exclui a tabela inline do CRT e mantém três paradas explícitas em referências a FIELDSTG. Esses alvos são `0x8008B770`, `0x8008BFA4` e `0x8008C000`; não há implementação fingida de overlay.

Resultado da geração: **zero marcadores de instrução inválida e zero stubs implícitos vazios de JAL**, contra 38.042 e 2.576 no perfil heurístico inicial. Isso elimina esses dois defeitos observados, mas não prova cobertura ou semântica correta de cada função.

O executável diagnóstico compilou e entrou na rotina principal. Emitiu comandos de GPU e transferências DMA de áudio. A execução limitada encontrou `VSync: timeout`; a captura de VRAM teve zero pixels não nulos, e o processo precisou ser encerrado no limite. Não foi validada uma tela de título, entrada, batalha ou save em jogo. Os serviços de IRQ/VBlank e a temporização do candidato são o próximo bloqueio dinâmico a investigar, sem assumir que trocar funções por no-ops seja uma solução.

Os endereços de carga da referência, ainda sujeitos a comparação com a tradução, são `0x80082CB0` para overlays de modo e `0x800A5DE0` para estágios. Isso corrige a direção da pesquisa: o scanner anterior estimava endereços por referências MIPS e seus números não devem ser usados como mapa final.

## Reprodução do diagnóstico

```powershell
.\scripts\build-candidate.ps1
.\scripts\build-boot-diagnostic.ps1
.\scripts\run-boot-diagnostic.ps1 -Seconds 10
```

O último comando tem limite de tempo, diretório próprio por execução, áudio dummy e dispatch estrito. Produz stdout, stderr, relatório e captura quando disponível, todos em `local/diagnostics/<id>/`. Timeout é reportado como erro, não como sucesso do jogo. O limite também pode interromper progresso lento; ele é um instrumento de diagnóstico e não um benchmark de velocidade.

O outro port [ArmDucks/DigimonWorld3-Recomp](https://github.com/ArmDucks/DigimonWorld3-Recomp/tree/95a7149635a225c23f0ae21fcae4670b893274d9) foi inspecionado como referência de arquitetura/persistência. Seu perfil público é USA/SLUS-01436, com layout diferente; ele não foi adotado nem tratado como compatível com este dump europeu traduzido.

## Progresso persistente

Foi implementado um [vault offline de memory cards e backups](SAVE_STORAGE.md), com testes de corrupção e falha de escrita. Também foi corrigido o caminho de gravação SIO do candidato para rejeitar imagens de tamanho incorreto e preservar o cartão anterior durante falhas de substituição. A implementação não equivale a save/load de uma campanha validada.

## Critérios ainda necessários

- Corrigir e testar a sincronização do caminho de boot, sem acelerar a simulação para esconder travamentos.
- Confirmar carregamento, identidade e ativação dos overlays da tradução.
- Alcançar e comparar título/menu e entrada contra uma execução de referência.
- Validar um percurso de gameplay com áudio, transições e batalha.
- Confirmar o protocolo de cartão usado pelo jogo, integrar o lease de escrita e o snapshot consistente e completar salvar → encerrar → reabrir → carregar.

4K, 160 FPS, Vulkan, overlay F1 e save states continuam nos marcos posteriores; nenhum desses recursos foi considerado entregue nesta etapa.
