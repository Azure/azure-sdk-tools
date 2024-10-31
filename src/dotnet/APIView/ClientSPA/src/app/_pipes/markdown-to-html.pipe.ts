import { Pipe, PipeTransform } from '@angular/core';
import { unified } from 'unified';
import { visit } from 'unist-util-visit';
import remarkGfm from 'remark-gfm';
import remarkParse from 'remark-parse';
import remarkRehype from 'remark-rehype';
import rehypeRaw from 'rehype-raw';
import rehypeStringify from 'rehype-stringify';
import rehypeHighlight from 'rehype-highlight';

const NON_SPECIAL_CHARACTERS = /^[^a-zA-Z0-9]+$/;

@Pipe({
  name: 'markdownToHtml'
})
export class MarkdownToHtmlPipe implements PipeTransform {
  transform(markdown: string, addLineActions: boolean = false): Promise<string> {
    return new Promise((resolve, reject) => {
      const processor = unified()
        .use(remarkParse)
        .use(remarkGfm)
        .use(remarkRehype, { allowDangerousHtml: true })
        .use(rehypeRaw)
        .use(rehypeHighlight);

      if (addLineActions) {
        processor.use(rehypeAddLineActions);
      }
      
      processor.use(rehypeStringify) 
        .process(markdown)
        .then((file) => {
          resolve(String(file));
        })
        .catch((err) => {
          reject(err);
        });
    });
  }
}

function rehypeAddLineActions() {
  return (tree: any) => {
    visit(tree, 'element', (node) => {
      if (node.tagName === 'pre' && node.children && node.children[0].tagName === 'code') {
        const codeNodes = node.children[0].children;
        const newChildren: any[] = [];
        let lineValue = "";
        let lastLineActionIndex = 0;

        newChildren.push(...getLineActions());

        codeNodes.forEach((codeNode: any) => {
          if (codeNode.type === 'text') {
            if (codeNode.value && (codeNode.value.includes('\n') || codeNode.value.includes('\r\n'))) {
              const lineParts = codeNode.value.split(/(\n|\r\n)/);
              lineParts.forEach((linePart: string, linePartIndex: number) => {
                newChildren.push({ type: 'text', value: linePart });
                lineValue += linePart;

                if (linePart === '\n' || linePart === '\r\n' && (linePartIndex != lineParts.length - 1)) {
                  if(lineValue.trim().length > 0 && !NON_SPECIAL_CHARACTERS.test(lineValue.trim())) {
                    // Ignore empty lines and lines with only special characters
                    newChildren[lastLineActionIndex].properties.title = lineValue.trim().replace(/"/g, '').replace(/'/g, "");
                    newChildren[lastLineActionIndex].children[0].properties.className.push('can-show');
                  }
                  else {
                    newChildren[lastLineActionIndex].children[0].properties.className.push('hide');
                  }
                  lineValue = "";

                  newChildren.push(...getLineActions());
                  lastLineActionIndex = newChildren.length - 1;
                }
              });
            } else {
              newChildren.push(codeNode);
              lineValue += codeNode.value;
            }
          } else {
            newChildren.push(codeNode);
            lineValue += getNodeText(codeNode);
          }
        });

        let index = newChildren.length - 1;
        while (index >= 0) {
          const node = newChildren[index];
          if (node) {
            if (node.type && node.type === 'text' && node.value.length > 0) {
              break;
            } else {
              newChildren.pop();
            }
          }
          index--;
        }

        node.children[0].children = newChildren;
      }
    });
  };
}

function getLineActions(): any[] {
  return [
    {
      type: 'element',
      tagName: 'span',
      properties: { className: ['line-actions'] },
      children: [
        {
          type: 'element',
          tagName: 'span',
          properties: { className: ['icon', 'bi', 'bi-chat-right-text', 'small', 'px-1', 'toggle-user-comments-btn'] },
          children: []
        }
      ]
    }
  ];
}

function getNodeText(node : any) : string {
  if (node.children) {
    return node.children.map(getNodeText).join('');
  }
  return node.value || '';
}