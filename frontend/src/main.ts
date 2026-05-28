import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

console.info('[OSD Angular] build', '2025-05-27', 'menu+iconos');

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
