import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface ProgressUpdate {
    progress: number;
    message: string;
}

@Injectable({
    providedIn: 'root'
})
export class SignalRService {
    private hubConnection: signalR.HubConnection | null = null;
    private progressSubject = new Subject<ProgressUpdate>();
    public progress$ = this.progressSubject.asObservable();

    constructor() { }

    async startConnection(): Promise<string> {
        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:7228/hubs/upload-progress', {
                withCredentials: false
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.hubConnection.on('ReceiveProgress', (progress: number, message: string) => {
            console.log('Progress received:', progress, message);
            this.progressSubject.next({ progress, message });
        });

        try {
            await this.hubConnection.start();
            console.log('SignalR Connected, ConnectionId:', this.hubConnection.connectionId);
            return this.hubConnection.connectionId || '';
        } catch (err) {
            console.error('Error starting SignalR connection:', err);
            throw err;
        }
    }

    async stopConnection(): Promise<void> {
        if (this.hubConnection) {
            await this.hubConnection.stop();
            console.log('SignalR Disconnected');
        }
    }

    getConnectionId(): string | null {
        return this.hubConnection?.connectionId || null;
    }
}
