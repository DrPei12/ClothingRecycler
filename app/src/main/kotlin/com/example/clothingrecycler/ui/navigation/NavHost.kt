package com.example.clothingrecycler.ui.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import com.example.clothingrecycler.ui.screens.customer.AddCustomerScreen
import com.example.clothingrecycler.ui.screens.customer.CustomerDetailScreen
import com.example.clothingrecycler.ui.screens.customer.CustomerListScreen
import com.example.clothingrecycler.ui.screens.home.HomeScreen
import com.example.clothingrecycler.ui.screens.inbound.InboundScreen
import com.example.clothingrecycler.ui.screens.order.OrderDetailScreen
import com.example.clothingrecycler.ui.screens.order.OrderListScreen
import com.example.clothingrecycler.ui.screens.outbound.OutboundScreen
import com.example.clothingrecycler.ui.screens.settings.SettingsScreen
import com.example.clothingrecycler.ui.screens.stock.StockScreen
import com.example.clothingrecycler.ui.screens.statistics.StatisticsScreen
import com.example.clothingrecycler.ui.screens.revenue.RevenueScreen

sealed class Screen(val route: String) {
    object Home : Screen("home")
    object Inbound : Screen("inbound")
    object Outbound : Screen("outbound")
    object Stock : Screen("stock")
    object Revenue : Screen("revenue")
    object OrderList : Screen("orders")
    object OrderDetail : Screen("order/{orderId}") {
        fun createRoute(orderId: Long) = "order/$orderId"
    }
    object Statistics : Screen("statistics")
    object Settings : Screen("settings")
    object CustomerList : Screen("customers")
    object CustomerDetail : Screen("customer/{customerId}") {
        fun createRoute(customerId: Long) = "customer/$customerId"
    }
    object AddCustomer : Screen("customer/add")
}

@Composable
fun ClothingRecyclerNavHost(
    navController: NavHostController = androidx.navigation.compose.rememberNavController()
) {
    NavHost(
        navController = navController,
        startDestination = Screen.Home.route
    ) {
        composable(Screen.Home.route) {
            HomeScreen(
                onNavigateToInbound = { navController.navigate(Screen.Inbound.route) },
                onNavigateToOutbound = { navController.navigate(Screen.Outbound.route) },
                onNavigateToStock = { navController.navigate(Screen.Stock.route) },
                onNavigateToRevenue = { navController.navigate(Screen.Revenue.route) },
                onNavigateToOrders = { navController.navigate(Screen.OrderList.route) },
                onNavigateToStatistics = { navController.navigate(Screen.Statistics.route) },
                onNavigateToSettings = { navController.navigate(Screen.Settings.route) },
                onNavigateToCustomers = { navController.navigate(Screen.CustomerList.route) }
            )
        }

        composable(Screen.Inbound.route) {
            InboundScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.Outbound.route) {
            OutboundScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.Stock.route) {
            StockScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.Revenue.route) {
            RevenueScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.Statistics.route) {
            StatisticsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.OrderList.route) {
            OrderListScreen(
                onNavigateBack = { navController.popBackStack() },
                onOrderClick = { orderId ->
                    navController.navigate(Screen.OrderDetail.createRoute(orderId))
                }
            )
        }

        composable(
            route = Screen.OrderDetail.route,
            arguments = listOf(navArgument("orderId") { type = NavType.LongType })
        ) { backStackEntry ->
            val orderId = backStackEntry.arguments?.getLong("orderId") ?: 0L
            OrderDetailScreen(
                orderId = orderId,
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.Settings.route) {
            SettingsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // 客户管理页面
        composable(Screen.CustomerList.route) {
            CustomerListScreen(
                onNavigateBack = { navController.popBackStack() },
                onCustomerClick = { customerId ->
                    navController.navigate(Screen.CustomerDetail.createRoute(customerId))
                },
                onAddCustomer = {
                    navController.navigate(Screen.AddCustomer.route)
                }
            )
        }

        composable(
            route = Screen.CustomerDetail.route,
            arguments = listOf(navArgument("customerId") { type = NavType.LongType })
        ) { backStackEntry ->
            val customerId = backStackEntry.arguments?.getLong("customerId") ?: 0L
            CustomerDetailScreen(
                customerId = customerId,
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable(Screen.AddCustomer.route) {
            AddCustomerScreen(
                onNavigateBack = { navController.popBackStack() },
                onCustomerAdded = { }
            )
        }
    }
}
