# Armazenamento de progresso — base do passo 3

Estado: armazenamento offline e correções de persistência SIO implementados e testados. **Ainda não existe uma sessão jogável de Digimon salvando e retomando progresso neste projeto.** O utilitário não é um sistema de save states; ele administra imagens de memory card.

## Ferramenta offline

```powershell
.\scripts\build-saves.ps1
.\build\Dw3.Saves.exe import '.\local\player-vault' '<sha256-da-imagem>' 1 'caminho\cartao.mcd'
.\build\Dw3.Saves.exe backup '.\local\player-vault' '<sha256-da-imagem>' 1
.\build\Dw3.Saves.exe list '.\local\player-vault' '<sha256-da-imagem>' 1
.\build\Dw3.Saves.exe restore '.\local\player-vault' '<sha256-da-imagem>' 1 '<id-do-backup>'
.\scripts\test-saves.ps1
```

Substituir o hash pelo SHA-256 real do conteúdo. O perfil traduzido e outro dump usam namespaces distintos; não presumir compatibilidade de progresso entre revisões. Slots aceitos: 1 e 2. O caminho de origem da importação não é alterado. A saída de sucesso é JSON; erro operacional retorna 1, uso incorreto retorna 2.

Esta versão opera somente offline, sem outro programa escrevendo no mesmo cartão. O runtime diagnóstico não usa automaticamente esta pasta nem seu lock. Não existe botão de backup no launcher ou overlay nesta etapa.

## Contrato e garantias verificadas

- Entrada: cartão raw de 128 KiB, assinatura `MC` e checksums XOR do cabeçalho e das 15 entradas de diretório. GME/VGS e outros containers precisam de conversão externa. Não validar o conteúdo do save de Digimon nem todas as cadeias do filesystem.
- Um lease exclusivo de arquivo impede dois clientes `SaveVault` de alterar o mesmo slot simultaneamente. Slots e hashes diferentes são isolados.
- Importação preserva um snapshot do cartão atual válido e um snapshot dos bytes recebidos antes da substituição. Um snapshot `import-candidate` indica bytes candidatos; sua presença não prova que a substituição do cartão atual terminou.
- Backup tem ID aleatório, versão, conteúdo, slot, data UTC, motivo, tamanho e SHA-256 do cartão. Dados e manifesto são escritos em diretório pendente e publicados ao final.
- Restauração valida ID, versão, namespace, slot, tamanho, formato e hash antes de modificar o cartão atual. O progresso atual é preservado antes da restauração.
- Cartão atual danificado, até 1 MiB, é preservado byte a byte em `recovery/` antes de recuperar o backup. Ele não aparece como backup válido na listagem. Arquivos maiores exigem preservação manual antes de restaurar.
- Gravação do cartão usa temporário no mesmo diretório, `Flush(true)` e substituição. Se a substituição falha, os testes confirmam que o cartão anterior permanece intacto e o temporário é removido.
- Transações interrompidas antes de publicar um backup deixam um diretório `.pending-*`, ignorado pela listagem. Nenhuma rotina remove automaticamente dados pendentes ou backups antigos nesta etapa.

```text
<vault>/<content-sha256>/slot1/
  .lock
  card.mcd
  backups/<id>/card.mcd
  backups/<id>/manifest.json
  recovery/<id>/...             bytes preservados de um cartão atual danificado
```

Retenção automática, migrações, interface visual, backup coordenado durante jogo e memória de sessão ficam para integração posterior. Os backups são append-only nesta versão, portanto ocupam espaço com o uso. Os testes simulam falhas de arquivo e corrupção; não simularam desligamento físico do equipamento.

## Correções no caminho SIO do candidato

O candidato já possui memory cards pelo SIO em `runtime/input`; sua existência não garante que todos os serviços BIOS necessários a Digimon estejam implementados. A leitura genérica de arquivos no BIOS cobre CD-ROM, enquanto vários serviços de cartão são stubs. Qual caminho Digimon usa durante gameplay ainda precisa de validação dinâmica.

Foram corrigidos no patch versionado:

1. `loadFromFile` exige exatamente 128 KiB e carrega para um buffer temporário; uma falha não modifica parcialmente a imagem em RAM. Cartões raw sem formatação continuam sendo representáveis pelo runtime para o jogo poder formatá-los.
2. `attachFile` não cria um cartão vazio por cima de uma imagem existente inválida ou ilegível. A falha deixa o cartão ausente e sem backing path gravável.
3. No Windows, o arquivo temporário é criado exclusivamente, recebe a imagem completa, passa por `FlushFileBuffers`, é fechado e substitui o destino com `MoveFileExW`. O nome do destino não é truncado antes da escrita.
4. Uma falha de persistência de setor retorna um status de escrita malsucedida ao guest; dados em RAM, flag e arquivo anterior são preservados. Escritas puramente em memória, sem backing file, continuam funcionando.

A atomicidade é por imagem de setor confirmada, não por uma operação completa de save do jogo: um save pode escrever vários setores. Snapshot antes/depois de um save completo ainda exige um ponto consistente e integração com o runtime.

O caminho não Windows usa temporário e rename, mas não recebeu a mesma validação de durabilidade feita para Windows. A coordenação de um escritor por perfil ainda precisa ser compartilhada pelo runtime e pelo vault; não anunciar backup ao vivo ou sessões concorrentes seguras antes dessa integração.

## Validação

`scripts/test-saves.ps1` cobre importação, reabertura, snapshots pré-restauração, escrita concorrente, substituição impedida por arquivo bloqueado, corrupção, identidade incorreta, travessia de caminho, cartão atual danificado, checksums e isolamento de slots/conteúdo.

`scripts/test-native-saves.ps1` compila contra a implementação SIO real do candidato e verifica persistência de um setor após reabrir, checksum inválido, falha de substituição no Windows, rollback da RAM, rejeição de cartões truncados e oversized. Os cartões e executáveis sintéticos ficam em `local/`, sem tocar em saves pessoais.

Referências de formato: [psx-spx — controladores e memory cards](https://psx-spx.consoledev.net/controllersandmemorycards/) e a implementação de referência inspecionada de [ArmDucks no commit 95a7149](https://github.com/ArmDucks/DigimonWorld3-Recomp/tree/95a7149635a225c23f0ae21fcae4670b893274d9/runtime). Nenhum código dessa implementação foi incorporado ao utilitário C#.
