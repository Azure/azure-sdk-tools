import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'languageNames'
})
export class LanguageNamesPipe implements PipeTransform {

  transform(language: string): string {
    if (language) {
      switch (language.toLocaleLowerCase())
      {
          case "c#":
              return "csharp";
          case "c++":
              return "cplusplus";
          default:
              return language.toLocaleLowerCase();
      }
    }
    return language;
  }

}
