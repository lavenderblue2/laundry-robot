class RealtimeRequestsManager {
    constructor() {
        this.refreshInterval = null;
        this.isRefreshing = false;
        this.lastUpdateTime = null;
        this.init();
    }

    init() {
        this.startAutoRefresh();
        this.bindEvents();
    }

    startAutoRefresh() {
        // Initial load
        this.refreshRequests();
        
        // Refresh every 1 second
        this.refreshInterval = setInterval(() => {
            this.refreshRequests();
        }, 1000);
    }

    stopAutoRefresh() {
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
            this.refreshInterval = null;
        }
    }

    async refreshRequests() {
        if (this.isRefreshing) return;
        
        this.isRefreshing = true;
        try {
            const response = await fetch('/api/requests-data');
            if (response.ok) {
                const data = await response.json();
                this.updateRequestsDisplay(data);
                this.updateStatsCards(data);
                this.lastUpdateTime = new Date();
            }
        } catch (error) {
            console.error('Error refreshing requests:', error);
        } finally {
            this.isRefreshing = false;
        }
    }

    updateStatsCards(data) {
        const pending = data.requests.filter(r => r.status === 'Pending').length;
        const active = data.requests.filter(r => r.status === 'Accepted' || r.status === 'InProgress').length;
        const completed = data.requests.filter(r => r.status === 'Completed').length;
        const declined = data.requests.filter(r => r.status === 'Declined').length;

        // Update stats display
        document.querySelectorAll('[data-stat="pending"]').forEach(el => el.textContent = pending);
        document.querySelectorAll('[data-stat="active"]').forEach(el => el.textContent = active);
        document.querySelectorAll('[data-stat="completed"]').forEach(el => el.textContent = completed);
        document.querySelectorAll('[data-stat="declined"]').forEach(el => el.textContent = declined);
    }

    updateRequestsDisplay(data) {
        const container = document.getElementById('requests-container');
        if (!container) return;

        if (data.requests.length === 0) {
            container.innerHTML = this.getEmptyStateHTML();
            return;
        }

        const requestsHTML = data.requests.map(request => this.getRequestHTML(request)).join('');
        container.innerHTML = requestsHTML;

        lucide.createIcons();
    }

    getRequestHTML(request) {
        const statusColor = this.getStatusColor(request.status);
        const statusIcon = this.getStatusIcon(request.status);
        const formattedStatus = this.formatStatus(request.status);

        return `
            <div class="px-6 py-6">
                <div class="flex items-center justify-between">
                    <div class="flex items-center space-x-4 flex-1">
                        <div class="flex-shrink-0">
                            <div class="h-12 w-12 rounded-xl bg-gradient-to-br from-brand-500/20 to-indigo-500/20 flex items-center justify-center group-hover:from-brand-500/30 group-hover:to-indigo-500/30 transition-all duration-200">
                                <i data-lucide="user" class="h-5 w-5 text-brand-400"></i>
                            </div>
                        </div>
                        <div class="min-w-0 flex-1">
                            <div class="flex items-start justify-between">
                                <div class="min-w-0 flex-1">
                                    <div class="flex items-center space-x-3">
                                        <div class="text-sm font-semibold text-white">Request #${request.id} - ${request.customerName}</div>
                                        ${request.totalCost ? `<div class="text-sm font-medium text-emerald-400">₱${request.totalCost.toFixed(2)}</div>` : ''}
                                    </div>
                                    <div class="flex items-center mt-1 space-x-4">
                                        <div class="text-sm text-slate-400 flex items-center">
                                            <i data-lucide="phone" class="w-3 h-3 mr-1"></i>
                                            ${request.customerPhone}
                                        </div>
                                        <div class="text-sm text-slate-400">${request.type.replace('AndDelivery', ' & Delivery')}</div>
                                    </div>
                                    <div class="text-sm text-slate-500 mt-1 flex items-center">
                                        <i data-lucide="map-pin" class="w-3 h-3 mr-1"></i>
                                        ${request.address}
                                    </div>
                                </div>
                                <div class="flex items-start space-x-6">
                                    <div class="text-right">
                                        <div class="text-sm text-slate-400">
                                            Requested: ${new Date(request.requestedAt).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' })}
                                        </div>
                                        <div class="text-sm text-slate-400">
                                            Scheduled: ${request.scheduledAt ? new Date(request.scheduledAt).toLocaleDateString('en-US', { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' }) : 'Not scheduled'}
                                        </div>
                                        ${request.weight ? `<div class="text-sm text-white font-medium">${request.weight} kg</div>` : ''}
                                        ${request.assignedRobotName ? `
                                            <div class="mt-2 inline-flex items-center px-2 py-1 bg-brand-900/50 text-brand-300 border border-brand-700/50 rounded-lg text-xs font-medium">
                                                <i data-lucide="bot" class="w-3 h-3 mr-1.5"></i>
                                                <span class="font-semibold">Robot:</span>
                                                <span class="ml-1">${request.assignedRobotName}</span>
                                            </div>
                                        ` : ''}
                                    </div>
                                    <div class="flex flex-col items-end space-y-3">
                                        <div class="relative group">
                                            <span class="px-3 py-2 inline-flex items-center text-xs font-semibold rounded-lg ${statusColor} cursor-help transition-all duration-200 hover:shadow-lg">
                                                <i data-lucide="${statusIcon}" class="w-3 h-3 mr-2 ${['InProgress', 'Washing', 'RobotEnRoute', 'FinishedWashingGoingToRoom'].includes(request.status) ? 'animate-spin' : ''}"></i>
                                                <span>${formattedStatus}</span>
                                            </span>
                                            <!-- Tooltip -->
                                            <div class="absolute bottom-full right-0 mb-2 hidden group-hover:block z-20 min-w-max">
                                                <div class="bg-slate-900 text-white text-xs rounded-lg py-2 px-3 shadow-xl border border-slate-700">
                                                    <div class="font-medium mb-1">${formattedStatus}</div>
                                                    <div class="text-slate-300 max-w-xs">${this.getStatusDisplay(request.status).description}</div>
                                                    <div class="absolute top-full right-4 w-0 h-0 border-l-4 border-r-4 border-t-4 border-l-transparent border-r-transparent border-t-slate-900"></div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="flex items-center space-x-3">
                                            ${this.getActionButtons(request)}
                                        </div>
                                    </div>
                                </div>
                            </div>
                            ${request.instructions ? `
                                <div class="mt-3 px-3 py-2 bg-slate-700/30 rounded-lg">
                                    <div class="text-xs text-slate-400 flex items-start">
                                        <i data-lucide="message-square" class="w-3 h-3 mr-2 mt-0.5 flex-shrink-0"></i>
                                        <span>${request.instructions}</span>
                                    </div>
                                </div>
                            ` : ''}
                            ${request.declineReason ? `
                                <div class="mt-3 px-3 py-2 bg-red-900/20 border border-red-700/30 rounded-lg">
                                    <div class="text-xs text-red-400 flex items-start">
                                        <i data-lucide="alert-circle" class="w-3 h-3 mr-2 mt-0.5 flex-shrink-0"></i>
                                        <span>Declined: ${request.declineReason}</span>
                                    </div>
                                </div>
                            ` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    getStatusColor(status) {
        const colors = {
            'Pending': 'bg-yellow-900/50 text-yellow-300 border border-yellow-700/50',
            'Accepted': 'bg-brand-900/50 text-brand-300 border border-brand-700/50',
            'InProgress': 'bg-purple-900/50 text-purple-300 border border-purple-700/50',
            'RobotEnRoute': 'bg-indigo-900/50 text-indigo-300 border border-indigo-700/50',
            'ArrivedAtRoom': 'bg-teal-900/50 text-teal-300 border border-teal-700/50',
            'LaundryLoaded': 'bg-cyan-900/50 text-cyan-300 border border-cyan-700/50',
            'ReturnedToBase': 'bg-blue-900/50 text-blue-300 border border-blue-700/50',
            'WeighingComplete': 'bg-violet-900/50 text-violet-300 border border-violet-700/50',
            'PaymentPending': 'bg-orange-900/50 text-orange-300 border border-orange-700/50',
            'Completed': 'bg-emerald-900/50 text-emerald-300 border border-emerald-700/50',
            'Declined': 'bg-red-900/50 text-red-300 border border-red-700/50',
            'Cancelled': 'bg-gray-900/50 text-gray-300 border border-gray-700/50',
            'Washing': 'bg-purple-900/50 text-purple-300 border border-purple-700/50',
            'FinishedWashing': 'bg-emerald-900/50 text-emerald-300 border border-emerald-700/50',
            'FinishedWashingReadyToDeliver': 'bg-amber-900/50 text-amber-300 border border-amber-700/50',
            'FinishedWashingGoingToRoom': 'bg-indigo-900/50 text-indigo-300 border border-indigo-700/50',
            'FinishedWashingArrivedAtRoom': 'bg-teal-900/50 text-teal-300 border border-teal-700/50',
            'FinishedWashingGoingToBase': 'bg-blue-900/50 text-blue-300 border border-blue-700/50',
            'FinishedWashingAwaitingPickup': 'bg-yellow-900/50 text-yellow-300 border border-yellow-700/50',
            'FinishedWashingAtBase': 'bg-emerald-900/50 text-emerald-300 border border-emerald-700/50'
        };
        return colors[status] || 'bg-slate-700 text-slate-300 border border-slate-600';
    }

    getStatusIcon(status) {
        const icons = {
            'Pending': 'clock',
            'Accepted': 'check',
            'InProgress': 'loader-2',
            'RobotEnRoute': 'truck',
            'ArrivedAtRoom': 'map-pin',
            'LaundryLoaded': 'package',
            'ReturnedToBase': 'home',
            'WeighingComplete': 'scale',
            'PaymentPending': 'credit-card',
            'Completed': 'check-circle',
            'Declined': 'x-circle',
            'Cancelled': 'ban',
            'Washing': 'loader-2',
            'FinishedWashing': 'check-circle-2',
            'FinishedWashingReadyToDeliver': 'package',
            'FinishedWashingGoingToRoom': 'truck',
            'FinishedWashingArrivedAtRoom': 'map-pin',
            'FinishedWashingGoingToBase': 'home',
            'FinishedWashingAwaitingPickup': 'package-check',
            'FinishedWashingAtBase': 'check-circle'
        };
        return icons[status] || 'circle';
    }

    getStatusDisplay(status) {
        const statusInfo = {
            'Pending': { display: 'Awaiting Approval', description: 'Request submitted, waiting for admin to accept' },
            'Accepted': { display: 'Approved', description: 'Request approved, robot will be dispatched soon' },
            'InProgress': { display: 'Processing', description: 'Request is being processed' },
            'RobotEnRoute': { display: 'Robot En Route', description: 'Robot is navigating to customer room' },
            'ArrivedAtRoom': { display: 'Robot Arrived', description: 'Robot has arrived at customer room for pickup' },
            'LaundryLoaded': { display: 'Laundry Loaded', description: 'Customer has loaded laundry, robot returning to base' },
            'ReturnedToBase': { display: 'Returned to Base', description: 'Robot has returned with laundry' },
            'WeighingComplete': { display: 'Weighing Complete', description: 'Laundry has been weighed' },
            'PaymentPending': { display: 'Payment Due', description: 'Waiting for customer payment' },
            'Completed': { display: 'Completed', description: 'Service completed successfully' },
            'Declined': { display: 'Declined', description: 'Request was declined by admin' },
            'Cancelled': { display: 'Cancelled', description: 'Request was cancelled' },
            'Washing': { display: 'Washing', description: 'Laundry is being washed' },
            'FinishedWashing': { display: 'Ready for Pickup', description: 'Laundry is clean and ready for pickup or delivery' },
            'FinishedWashingReadyToDeliver': { display: 'Ready to Deliver', description: 'Customer chose delivery - admin needs to load laundry on robot' },
            'FinishedWashingGoingToRoom': { display: 'Delivery in Progress', description: 'Robot is delivering clean laundry to customer' },
            'FinishedWashingArrivedAtRoom': { display: 'Delivery Arrived', description: 'Robot has arrived with clean laundry' },
            'FinishedWashingGoingToBase': { display: 'Returning to Base', description: 'Robot is returning after delivery' },
            'FinishedWashingAwaitingPickup': { display: 'Ready for Pickup', description: 'Clean laundry is ready for customer pickup' },
            'FinishedWashingAtBase': { display: 'At Base - Ready to Complete', description: 'Clean laundry delivered, awaiting admin completion' }
        };
        return statusInfo[status] || { display: status, description: 'Status update' };
    }

    formatStatus(status) {
        return this.getStatusDisplay(status).display;
    }

    getActionButtons(request) {
        let buttons = '';
        
        if (request.status === 'Pending') {
            buttons += `
                <button onclick="showAcceptModal(${request.id})" 
                        class="inline-flex items-center px-4 py-2 bg-emerald-600 text-white hover:bg-emerald-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                    <i data-lucide="check" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                    <span>Accept</span>
                </button>
                <button onclick="showDeclineModal(${request.id})"
                        class="inline-flex items-center px-4 py-2 bg-red-600 text-white hover:bg-red-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                    <i data-lucide="x" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                    <span>Decline</span>
                </button>
            `;
        }
        
        if (request.status === 'Washing') {
            buttons += `
                <button onclick="showMarkForPickupDeliveryModal(${request.id})"
                        class="inline-flex items-center px-4 py-2 bg-indigo-600 text-white hover:bg-indigo-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                    <i data-lucide="package-check" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                    <span>Mark for Pickup/Delivery</span>
                </button>
            `;
        }

        if (request.status === 'FinishedWashingReadyToDeliver') {
            buttons += `
                <form action="/Requests/StartDelivery" method="post" class="inline">
                    <input type="hidden" name="requestId" value="${request.id}"/>
                    <button type="submit"
                            class="inline-flex items-center px-4 py-2 bg-brand-600 text-white hover:bg-brand-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                        <i data-lucide="truck" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                        <span>Start Delivery</span>
                    </button>
                </form>
            `;
        }

        if (['Accepted', 'InProgress', 'ReturnedToBase', 'FinishedWashingAtBase', 'FinishedWashingAwaitingPickup'].includes(request.status)) {
            buttons += `
                <form action="/Requests/CompleteRequest" method="post" class="inline">
                    <input type="hidden" name="requestId" value="${request.id}"/>
                    <button type="submit"
                            class="inline-flex items-center px-4 py-2 bg-emerald-600 text-white hover:bg-emerald-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                        <i data-lucide="check-circle" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                        <span>Complete</span>
                    </button>
                </form>
            `;
        }
        
        buttons += `
            <a href="/Requests/Details/${request.id}"
               class="inline-flex items-center px-4 py-2 bg-slate-600 text-white hover:bg-slate-700 text-sm font-semibold rounded-lg transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                <i data-lucide="external-link" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                <span>Details</span>
            </a>
        `;
        
        return buttons;
    }

    getEmptyStateHTML() {
        return `
            <div class="px-6 py-16 text-center">
                <div class="w-20 h-20 bg-slate-700/50 rounded-3xl flex items-center justify-center mx-auto mb-6">
                    <i data-lucide="inbox" class="h-10 w-10 text-slate-500"></i>
                </div>
                <h3 class="text-xl font-medium text-white mb-3">No Requests Yet</h3>
                <p class="text-slate-400 mb-8 max-w-md mx-auto">Customer laundry requests will appear here once they start submitting them.</p>
                <button class="inline-flex items-center px-6 py-3 bg-gradient-to-r from-brand-600 to-indigo-600 hover:from-brand-700 hover:to-indigo-700 text-white text-sm font-medium rounded-xl transition-all duration-200 shadow-lg hover:shadow-xl whitespace-nowrap">
                    <i data-lucide="plus" class="w-4 h-4 mr-2 flex-shrink-0"></i>
                    <span>Create First Request</span>
                </button>
            </div>
        `;
    }

    bindEvents() {
        // Stop refresh when user is interacting with modals
        document.addEventListener('click', (e) => {
            if (e.target.closest('#acceptModal, #declineModal, #markForPickupDeliveryModal')) {
                this.stopAutoRefresh();
            }
        });

        // Resume refresh when modals are closed
        window.addEventListener('modalClosed', () => {
            this.startAutoRefresh();
        });
    }

    destroy() {
        this.stopAutoRefresh();
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.realtimeManager = new RealtimeRequestsManager();
});

// Clean up on page unload
window.addEventListener('beforeunload', () => {
    if (window.realtimeManager) {
        window.realtimeManager.destroy();
    }
});