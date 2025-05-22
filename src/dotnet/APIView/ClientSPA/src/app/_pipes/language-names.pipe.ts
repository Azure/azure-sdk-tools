import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'languageNames'
})
export class LanguageNamesPipe implements PipeTransform {

  transform(language: string, theme: string | undefined = undefined): string {
    if (language) {
      switch (language.toLocaleLowerCase())
      {
          case "c#":
            return "csharp";
          case "c++":
            return "cplusplus";
          case "rust":
            if (theme && theme === "light-theme") {
              return "rust";
            } else {
              return "rust-light";
            }
          default:
            return language.toLocaleLowerCase();
      }
    }
    return language;
  }

}
