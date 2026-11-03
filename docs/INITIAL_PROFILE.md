# Perfil inicial de pesquisa

Observado em 2026-11-03, usando a cópia local fornecida pelo proprietário do projeto. O nome do arquivo informa BR-EUR/T1.01; autoria, revisão do patch e equivalência com uma imagem sem modificação não foram verificadas.

O perfil público contém apenas hashes e metadados, em `profiles/dw3-sles04936-local-translation.json`. O manifesto completo e o executável continuam em `local/prepared/`, ignorados pelo Git.

| Propriedade | Valor observado |
| --- | --- |
| Estado | Pesquisa; boot nativo e gameplay não verificados |
| Volume | `DMW3` |
| Boot no disco | `SLES_049.36` |
| Layout | CUE de uma faixa, MODE2/2352, INDEX 01 00:00:00 |
| Tamanho da imagem BIN | 692.146.560 bytes |
| SHA-256 da imagem BIN | `cba308798bf768da4c0675f3130a4b380f02b53da1a50a7537ad9be7d8f3da94` |
| SHA-256 do boot completo | `47804642b50bbc0e39418fadce9dd89843e8c2f5e780839ca7e943653712b4ae` |
| Tamanho do boot | 923.648 bytes |
| Cabeçalho PS-X EXE | 2.048 bytes |
| Payload carregado | 921.600 bytes (`0xE1000`) |
| Endereço de carga | `0x80010000` |
| Fim exclusivo da carga | `0x800F1000` |
| Ponto de entrada | `0x80010E90` |
| Offset da entrada no payload | `0xE90` |
| Offset da entrada no arquivo | `0x1690` |
| Inventário ISO | 2.482 entradas, incluindo diretórios |

A região carregada inclui código e dados; não tratar todo o payload como instruções. Extensões `.BIN` não identificam automaticamente overlays. Não foram determinados limites de funções, imports de BIOS, callbacks, jump tables, compactação nem módulos executáveis internos. O hash da imagem crua não identifica uma ISO convertida byte a byte: uma cópia reempacotada precisa de outro perfil, ainda que compartilhe boot.

## Reproduzir a verificação

Na raiz do repositório, extrair o arquivo local e preparar o CUE:

```powershell
.\scripts\build.ps1
.\build\Dw3.Launcher.exe prepare 'caminho\da\copia.cue' '.\local\prepared'
.\scripts\verify-profile.ps1
.\scripts\test.ps1
```

O verificador compara o manifesto com o perfil e recalcula SHA-256 da imagem real e do boot extraído; também confere tamanhos e metadados. Pode receber `-ManifestPath` e `-ProfilePath`. Não executa o boot nem valida funcionalmente o jogo. Para regenerar o inventário a partir da imagem, executar novamente `prepare`; o verificador não substitui essa leitura.

## Próxima investigação

1. Inspecionar o ponto de entrada e separar inicialização, código e dados alcançáveis.
2. Localizar caminhos de leitura do CD e carregamentos em RAM para identificar overlays por evidência.
3. Registrar chamadas BIOS/PsyQ e operações GTE usadas, sem presumir que todas aceitam substituição HLE.
4. Resolver destinos de chamadas indiretas e catalogar o que depende de observação dinâmica.
5. Medir temporização e gerar uma referência de boot antes de alterar FPS ou resolução interna.

Mapas e trechos derivados do jogo permanecem em `local/analysis/<image-hash>/`. O repositório recebe ferramentas, decisões e metadados revisados.
