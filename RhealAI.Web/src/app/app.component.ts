import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'Rheal AI Project Inspector';

  menuItems = [
    { path: '/upload', icon: 'cloud_upload', label: 'Upload Repository' },
    { path: '/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/standards', icon: 'rule', label: 'Standards' },
    { path: '/reports', icon: 'assessment', label: 'Reports' }
  ];
}
