# Revisão visual e de experiência

Validação final realizada em 12/09/2026 no aplicativo macOS empacotado, em uma janela de 1052 por 768 pontos e no tema claro e escuro.

## Direção visual

A interface usa a identidade aprovada em `identidade-visual/` e toma como referência os princípios de organização do ChatGPT para macOS: navegação lateral estável, ação principal evidente, alternância de modo no topo, conteúdo central com largura confortável e ações secundárias com divulgação progressiva. A composição, as cores, o símbolo e os textos permanecem próprios do Valerius AI.

## Verificações concluídas

- Marca, nome do produto e controles da sidebar estão alinhados verticalmente.
- Chat e Criar compartilham um controle segmentado centralizado e com a mesma altura.
- Biblioteca e Busca compartilham uma linha de ícones pequenos com nomes acessíveis e tooltips.
- Arquivadas, Ajustes e Créditos compartilham uma linha de três ícones discretos no rodapé.
- O texto “Local por natureza” foi movido para a tela de informações, junto com privacidade e créditos.
- O estado vazio, o composer, o botão de envio e as sugestões usam um mesmo eixo visual.
- Mensagens longas mantêm largura de leitura, seleção, cópia, Markdown e rolagem.
- Durante streaming, o campo é desabilitado e o botão de envio muda para Interromper geração.
- A interrupção deixa uma resposta parcial identificada e devolve o foco ao composer.
- A Biblioteca organiza Conhecimento, Arquivos criados e Memória no mesmo nível.
- Tools e MCP ficam em Recursos avançados dentro de Ajustes.
- O menu de conversa apresenta Renomear, Mover para uma pasta, Fixar, Arquivar e Excluir com larguras consistentes.
- A escolha de pasta apresenta Sem pasta e as pastas disponíveis em uma lista simples.
- A tela de créditos destaca Guibson Valerio, `guibson.com.br`, Valerius Studios e `valeriusstudios.com`.
- O tema escuro foi aplicado, inspecionado e a preferência Sistema foi restaurada ao final.
- A largura mínima é 700 e a altura mínima é 560. O modo compacto reduz sidebar e margens sem remover ações essenciais.

## Acessibilidade

Os ícones possuem nomes acessíveis, foco por teclado e tooltips. Campos e seletores têm labels, os botões essenciais mantêm área de interação adequada e os temas usam tokens separados para contraste. A árvore de acessibilidade foi inspecionada nos estados vazio, conversa, streaming, interrupção, Biblioteca, Ajustes e Créditos.

## Evidência funcional associada

No mesmo pacote revisado, uma mensagem foi enviada ao `qwen3:1.7b`, os tokens apareceram progressivamente, a resposta terminou e a conversa reapareceu após fechar e reabrir o `.app`. Uma segunda geração foi interrompida pela interface. O modo Criar produziu um PDF A4 válido e o registrou em Arquivos criados.
