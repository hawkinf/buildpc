# Instruções permanentes para agentes e ferramentas de IA

1. Leia `CONTEXTO_DO_PROJETO.md` completamente antes de alterar este
   repositório.
2. Toda alteração funcional, visual, técnica, de banco, API, dependência,
   execução ou implantação deve atualizar `CONTEXTO_DO_PROJETO.md` no mesmo
   commit.
3. Não declare contagens de testes sem executar a suíte atualizada.
4. Preserve mudanças existentes que não pertençam à tarefa.
5. Execute `git diff --check`, compile a solução e rode os testes antes de
   concluir.
6. O projeto de interface usado pelo usuário é `BuildPc.Desktop`;
   `BuildPc.Api` é apenas o servidor privado.
7. A única Montagem é `FlexibleListView`; a montagem antiga baseada em `Slots`
   foi removida de `MainWindow.axaml`.
8. Nunca exponha chaves de API, senhas, strings de conexão ou outros segredos.
9. O usuário exige commit e push para `origin/main` ao final de cada
   solicitação concluída.
