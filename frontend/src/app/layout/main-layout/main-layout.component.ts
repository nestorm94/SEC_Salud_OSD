import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';
import { BreadcrumbsComponent } from '../../shared/components/breadcrumbs/breadcrumbs.component';
import { AuthService } from '../../core/auth/auth.service';
import { SessionKeepAliveService } from '../../core/auth/session-keepalive.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, HeaderComponent, BreadcrumbsComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly sessionKeepAlive = inject(SessionKeepAliveService);

  readonly sidebarOpen = signal(false);

  ngOnInit(): void {
    this.sessionKeepAlive.start();
    this.auth.refrescarSesion()?.subscribe({ error: () => {} });
  }

  ngOnDestroy(): void {
    this.sessionKeepAlive.stop();
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }
}
