#import "template.typ": *

#show: template.with(
  title: "Buriza Wallet: UI/UX Design and Front-End Development",
  company: "SAIB Inc.",
  date: datetime.today().display(),
  main-color: "#4F8EFF",
  authors: [Clark Alesna#super[1] #h(20pt) Stan Kiefer Gallego#super[2] #h(20pt) Caitlin Lindsay#super[3]],
  emails: (
    "clark@saib.dev", "stan.gallego@saib.dev", "caitlin.lindsay@saib.dev"
  ),
)

 #outline(
    title: [Table of Contents],
    depth: 3,
    indent: 2em,
)

#pagebreak()

= Introduction

Buriza is a comprehensive Cardano wallet software suite spanning mobile, desktop, and browser platforms. The name Buriza is inspired by the Japanese interpretation of "blizzard," symbolizing power, resilience, and control. Reflecting this spirit, Buriza empowers users with full sovereignty over their wallet infrastructure through open-source development and complete decentralization.

Funded under Project Catalyst Fund 13, the initiative is structured across three complementary proposals:
- #link("https://milestones.projectcatalyst.io/projects/1300170/milestones/1")[*Buriza.Mobile*] — focused on delivering a flexible and responsive mobile application,
- #link("https://milestones.projectcatalyst.io/projects/1300168/milestones/1")[*Buriza.Browser*] — the browser extension interface, and
- #link("https://milestones.projectcatalyst.io/projects/1300169/milestones/1")[*Buriza.Desktop*] — a desktop implementation featuring full-node capabilities.

Since Milestone 1, Buriza’s design has undergone significant evolution. The team adopted Material Design 3 (MD3) principles to create a more refined, accessible, and cohesive user experience. A major visual shift includes the introduction of Buriza Frostling, a bold, yeti-inspired mascot that replaces the earlier soft, gem-like designs. Frostling embodies Buriza’s core values—security, trust, and power—and appears throughout the interface to reinforce brand consistency.

The updated design also features enhanced iconography, a standardized color system based on Buriza’s three original core colors, and new accent shades that provide stronger visual contrast. Clean mockups and improved UI patterns further highlight the wallet’s sleek, modern aesthetic.

On the technical side, Buriza is built on a modular .NET 9 architecture, emphasizing shared components and platform independence. At the core of the system is the Buriza.UI shared component library, enabling a single codebase to serve multiple targets—browser extension, progressive web app (PWA), and cross-platform MAUI applications. This approach reduces redundant development and ensures a consistent user experience across all platforms.

The front-end implementation utilizes:
- Blazor for application logic and rendering,
- MudBlazor for MD3-compliant UI components, and
- Tailwind CSS for utility-first styling.

The browser extension is powered by Blazor WebAssembly and follows Manifest V3 standards. The PWA achieves near-native performance through WebAssembly, while the MAUI app leverages BlazorWebView to combine native device capabilities with a unified web-based UI.

With these design and architectural foundations in place, this document outlines the progress and deliverables completed for Milestone 2. It details the evolution of the user interface and system architecture since Milestone 1, explains the project setup and tools used, and documents the front-end implementation methodology. Screenshots, videos, and working examples are provided to demonstrate Buriza’s evolving functionality. The report also highlights the challenges encountered and the solutions developed by the SAIB Inc. team, reflecting its ongoing commitment to building a secure, performant, and user-centered Cardano wallet.

This report has been converted to Word format for Project Catalyst submission. The original version may be found in the Buriza project's GitHub repository:
#link("https://github.com/SAIB-Inc/Buriza/tree/main/src/Buriza.Docs")[https://github.com/SAIB-Inc/Buriza/tree/main/src/Buriza.Docs]

#pagebreak()

= Buriza Front-End 
This section covers the branding and UI/UX design of the Buriza wallet, describing the updates made since the first submission.
Additionally, it tackles the implementation of the design - detailing the project set up and various tools utilized.

== Design
This section outlines Buriza’s design evolution in alignment with the principles and best practices of Google’s Material Design 3.

Buriza's design has significantly evolved since the submission of Project Catalyst's Milestone 1,
with the initial branding and UI/UX designs detailed in the following 
#link("https://saibph-my.sharepoint.com/:b:/g/personal/accounts_saib_dev/Ea17Rbt3BLlFvIbwLlNMhMcB0LgqVCZc94Jx55p-FHo8zw?e=KheTGY")[report].

A key transformation in Buriza's branding is the introduction of its new mascot, Buriza Frostling. 
Inspired by the yeti, Frostling’s energetic expressions and bold, resilient personality stand in contrast 
to the earlier delicate, pastel mascots. This strong character is present throughout the application’s 
design, reinforcing Buriza’s core values of security, trust, and power.

The updated branding report also features enhanced iconography, an expanded color palette, 
and clean mockups that showcase Buriza’s modern and sleek aesthetic. These comprehensive changes can 
be viewed in the revised 
#link("https://www.figma.com/design/YVzczU3XoKagofQP5gOQZI/Buriza?node-id=1151-1432")[branding
report], which includes detailed descriptions and illustrations of the logo and typography.

#v(2em)

#figure(
  image("images/buriza_mockup.png"),
  caption: [Buriza Mockup: featuring the mascot, colors, and logo]
)

These developments are reflected in the overall UI and UX of the wallet, resulting in a more refined and visually cohesive application. The improvements stem from the opportunity the SAIB Inc. design team had to explore Material Design 3, using its guidelines to further elevate the project's look and feel. These refinements can be seen in detail in the following #link("https://www.figma.com/design/9Bw3Nuh8UF9xYHkURwJeoI/Buriza-with-MD3?node-id=379-125141&t=cVzlkeRQmhBVQk0d-1")[Figma
 file].

 #pagebreak();

=== Material Design 3

Material Design is a design system created by Google designers and developers that provides UX foundations,
 guidance, and UI components across Android, Flutter, and the Web. Its most recent update, Material Design 
 3, highlights emotional impact. Placing great significance on expressiveness, dynamism, and accessibility,  
 Material Design 3 expands the system with additional shapes, typography guidelines, intuitive motions, 
 and more (#link("https://m3.material.io/")[Material Design 3], 2025).

SAIB's design team studied the foundations of this update, noting and implementing changes based on Material Design 3's guidelines on color, elevation, icons, shape, motion, and typography. The impact of this study can be seen in the most recent design. Buriza's colors have been evaluated for contrast, and the initial color scheme has been updated to a more vibrant palette with accents and a greater variety of shades established for uniformity. Elevation is utilized to provide visual hierarchy and interaction guides for users, and labels have been added to the primary menu icons. Similar considerations have been applied to the wallet's shapes, with Material Design 3 button group designs added to many main pages and expressive animations applied in the main header. While Buriza's initial typography has not changed, revisions have been made to consider type roles within pages.

#v(2em)

#figure(
  image("images/buriza_home_evolution.png", width: 80%),
  caption: [Buriza Mobile UI - Home: design evolution from first to third iteration (left to right)]
)

#figure(
  image("images/buriza_history_evolution.png", width: 80%),
  caption: [Buriza Mobile UI - History: design evolution from first to third iteration (left to right)]
)

#v(3em)

#figure(
  image("images/buriza_send_evolution.png"),
  caption: [Buriza Desktop UI - Send Assets: design evolution from first to third iteration (left to right)]
)

#v(2em)

Buriza's latest design update embodies SAIB's desire to create an impactful application that continues to evolve and improve. The team strives for continuous design iteration and improvement. With the future release of prototypes or mockups to the public, SAIB will continue to evolve through community feedback, validating users' needs and experiences while augmenting improvements with design solutions backed by research and best practices.

#pagebreak()

== Development

This section covers the technical implementation of Buriza's wallet suite, including architecture decisions, platform implementations, and front-end technologies.

=== Project Architecture

Buriza's architecture is built around component sharing and platform independence, enabling a single codebase to serve multiple deployment targets.

==== Solution Structure

Buriza uses a modular architecture with 5 .NET 9 projects designed for scalability and maintainability:

- *`Buriza.UI`* - Shared Blazor component library containing all user interface elements
- *`Buriza.Data`* - Core data models and services for wallet functionality
- *`Buriza.Extension`* - Browser extension for seamless dApp interaction
- *`Buriza.Web`* - Progressive web app for universal access
- *`Buriza.App`* - Cross-platform MAUI app for mobile and desktop

This architecture eliminates the need to implement separate UI layers for each platform. Instead of building distinct interfaces for the browser extension, web app, and mobile app, all platforms consume the same *`Buriza.UI`* components. This approach ensures consistent user experience while dramatically reducing development effort and maintenance overhead.

==== Shared Component Library (*`Buriza.UI`*)

The component library provides a comprehensive set of reusable Blazor components organized into a hierarchical structure:

- *Common* - Foundational UI primitives like buttons, text fields, and navigation tabs
- *Controls* - Advanced interactive elements including asset cards and search functionality
- *Layout* - Application structure components for sidebars and main content areas
- *Pages* - Complete page/screen implementations for wallet operations like assets, transaction history, dapp access, send, and receive

The library leverages MudBlazor's Material Design 3 implementation alongside Tailwind CSS for utility-first styling and responsive design patterns. This combination ensures both visual consistency and flexible customization across all platform implementations.

=== Platform Implementations

Each platform implementation leverages the shared *`Buriza.UI`* components while providing platform-specific features and deployment mechanisms.

==== Browser Extension

The browser extension represents Buriza's most integrated approach to decentralized web interaction, embedding wallet functionality directly into the user's browsing experience. Built with Blazor WebAssembly and the Blazor.BrowserExtension framework, the extension follows Manifest V3 standards to ensure compatibility with modern browser security requirements and enhanced performance through service workers.

==== Progressive Web App

The progressive web application serves as Buriza's most accessible deployment target, requiring only a modern web browser to provide full wallet functionality. Built with Blazor WebAssembly, the PWA delivers near-native performance by compiling C\# code to WebAssembly bytecode that executes directly in the browser's runtime environment.

The PWA architecture enables installation directly from the browser without requiring app store distribution, creating a native-like experience while maintaining web-based deployment advantages.

==== Cross-Platform App

The #link("https://learn.microsoft.com/en-us/dotnet/maui/")[MAUI] application targets iOS, Android, macOS, and Windows through a hybrid architecture that combines native platform capabilities with web-based UI components. At the core of this implementation is BlazorWebView, a native control that hosts Blazor content within a webview container while providing seamless integration with platform-specific APIs.

BlazorWebView acts as a bridge between the native MAUI shell and the Blazor UI layer, allowing the same *`Buriza.UI`* components to render within a native application context. This approach eliminates the need to rebuild the entire user interface using platform-specific controls like XAML, while still providing access to native device features such as secure storage, biometric authentication, and push notifications.

The hybrid model significantly reduces development complexity by leveraging existing web technologies and shared component libraries. Rather than maintaining separate native codebases for each platform, the application shares a single UI implementation while the MAUI framework handles platform-specific compilation and native API integration automatically.

=== Front-End Implementation

The front-end implementation combines modern web technologies with component-driven architecture to deliver consistent user experiences across all platform deployments.

==== Blazor Components

#link("https://learn.microsoft.com/en-us/aspnet/core/blazor/")[Blazor] is Microsoft's web framework that serves as the foundational UI technology for Buriza, enabling C\# development for web interfaces through WebAssembly compilation. This Microsoft-developed framework allows the entire Buriza application stack to use a single programming language, eliminating context switching between backend and frontend development.

The component architecture follows a hierarchical structure where complex UI elements are composed of smaller, reusable primitives. Blazor's two-way data binding simplifies form interactions and real-time updates, particularly important for wallet operations that require immediate visual feedback on transaction states and balance changes.

==== MudBlazor Design System

#link("https://mudblazor.com/")[MudBlazor] v8.11.0 provides the Material Design 3 foundation for Buriza's visual language, delivering pre-built components with accessibility standards and smooth animations. The theming system integrates seamlessly with CSS custom properties, allowing Buriza to maintain brand identity while leveraging Material Design's proven usability patterns.

Buriza extends this foundation with Buriza-styled components:

*Common Components:*
- *BurizaButton* - Standardized button with consistent styling and interaction states
- *BurizaTextField* - Custom text input with validation and wallet-specific formatting
- *BurizaTabs* - Navigation tabs with custom styling for asset type switching
- *BurizaSelect* - Dropdown selection with wallet account and asset filtering
- *BurizaHeader* - Consistent header layout across all pages

*Control Components:*
- *BurizaAssetCard* - Asset display with balance, price changes, and interactive actions
- *BurizaSearchBar* - Universal search functionality for assets and transactions
- *BurizaDappCard* - dApp connection interface with authorization controls

==== Tailwind CSS Integration

#link("https://tailwindcss.com/")[Tailwind CSS] v4 complements MudBlazor by providing utility-first styling capabilities for custom layouts and responsive behavior. The integration uses a Bun build pipeline for rapid CSS compilation and automatic purging of unused styles, ensuring optimal bundle sizes.

The utility-first approach enables precise control over spacing, positioning, and responsive breakpoints, particularly valuable for wallet interfaces that require exact alignment for transaction details and balance displays. Custom CSS variables bridge the gap between Tailwind utilities and MudBlazor's theming system.

==== Responsive Design Patterns

Buriza implements a mobile-first responsive design strategy that adapts to various screen sizes and interaction methods. The design system accommodates everything from mobile browser extensions to desktop applications while maintaining usability and visual hierarchy.

The responsive approach extends beyond screen size to consider platform-specific interaction patterns. Touch targets are appropriately sized for mobile interfaces, while desktop versions support keyboard navigation and hover states, ensuring consistent functionality regardless of how users access Buriza.

#pagebreak()

= Implementation

This section outlines the 
implementation progress and highlights challenges encountered by the development team.

Buriza’s unique infrastructure significantly streamlined the development process. Rather than 
maintaining separate codebases for each platform and operating system, the unified Buriza.UI codebase 
ensures compatibility across mobile, desktop, and web platforms—spanning Windows, macOS, Linux, Android, 
and iOS. As a result, platform-specific development primarily focused on using Tailwind’s 
responsive directives to adapt the user interface across varying screen sizes.

To ensure both code quality and visual fidelity, the development process established had code peer-reviewed prior to being pushed to the repository. The team maintained a strong focus on building a beautiful, high-quality application through continuous collaboration and refinement.

== Mobile
Development began with a mobile-first approach, with the goal of closely following the design system 
established by the design team. The focus was on replicating the intended user experience, layout 
behavior, and visual responsiveness on mobile devices.

During this process, the team encountered a challenge related to how image assets were handled. Since the 
Buriza.App project (used to compile the mobile application) did not have direct access to image files 
stored in the shared Buriza.UI library, assets such as icons and illustrations could not be referenced at 
runtime.

To solve this, the team developed an internal utility that enables asset embedding directly into the 
application code. This ensured that all necessary visual elements remained accessible regardless of 
platform-specific limitations—eliminating the need for runtime file system access and enabling smooth, 
consistent rendering across platforms.


== Browser
Browser extension development leveraged the shared codebase, with only minor layout adjustments 
needed to fit the extension's popup dimensions. When fully expanded, the extension mirrors the desktop 
interface.

Similar to the mobile application, the extension faced challenges with accessing shared image assets once 
deployed in the browser environment. Since browser extensions operate in isolated environments with 
limited file system access, the same asset management solution used in the mobile app was also applied 
here. This unified approach allowed the team to maintain consistency in asset rendering without 
duplicating resources or code.

== Desktop
The desktop implementation also leveraged Blazor’s flexible layout system. Developers were able to 
implement the drawer-based interface with minimal overhead, using dynamic layouts that adjusted based on 
page context.

Additionally, core features such as Send and Receive reused mobile UI patterns within drawers, allowing the team to 
focus on small adjustments rather than re-implementing entire views.

#pagebreak()

= User Interface

== Mobile

=== Send Assets

#figure(
  image("images/screenshots/mobile/send_assets_dark.png",width:50%),
  caption: [Buriza UI - Mobile: Send assets]
)

#figure(
  image("images/screenshots/mobile/send_assets_light.png"),
  caption: [Buriza UI - Mobile: Send assets]
)

#figure(
  image("images/screenshots/mobile/send_assets_add_recipient_dark.png"),
  caption: [Buriza UI - Mobile: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/mobile/send_assets_add_recipient_light.png"),
  caption: [Buriza UI - Mobile: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/mobile/send_assets_add_token_dark.png"),
  caption: [Buriza UI - Mobile: Send assets with an additional token]
)

#figure(
  image("images/screenshots/mobile/send_assets_add_token_light.png"),
  caption: [Buriza UI - Mobile: Send assets with an additional token]
)

#figure(
  image("images/screenshots/mobile/send_assets_transaction_summary_dark.png"),
  caption: [Buriza UI - Mobile: Send assets transaction summary]
)

#figure(
  image("images/screenshots/mobile/send_assets_transaction_summary_light.png"),
  caption: [Buriza UI - Mobile: Send assets transaction summary]
)

#figure(
  image("images/screenshots/mobile/send_assets_transaction_summary_add_recipient_dark.png"),
  caption: [Buriza UI - Mobile: Send assets transaction summary with additional recipient]
)

#figure(
  image("images/screenshots/mobile/send_assets_transaction_summary_add_recipient_light.png"),
  caption: [Buriza UI - Mobile: Send assets transaction summary with additional recipient]
)

#figure(
  image("images/screenshots/mobile/face_id_dark.png"),
  caption: [Buriza UI - Mobile: Face ID]
)

#figure(
  image("images/screenshots/mobile/face_id_light.png"),
  caption: [Buriza UI - Mobile: Face ID]
)

#figure(
  image("images/screenshots/mobile/send_assets_success_screen_dark.png"),
  caption: [Buriza UI - Mobile: Send assets success screen]
)

#figure(
  image("images/screenshots/mobile/send_assets_success_screen_light.png"),
  caption: [Buriza UI - Mobile: Send assets success screen]
)

=== Receiving Assets

#v(2em)

#figure(
  image("images/screenshots/mobile/recieve_assets_qr_code_dark.png",width: 50%),
  caption: [Buriza UI - Mobile: Receive assets]
)

#figure(
  image("images/screenshots/mobile/receive_assets_qr_code_light.png"),
  caption: [Buriza UI - Mobile: Receive assets]
)

#figure(
  image("images/screenshots/mobile/receive_assets_advanced_mode_dark.png"),
  caption: [Buriza UI - Mobile: Receive assets advanced mode]
)

#figure(
  image("images/screenshots/mobile/receive_assets_advanced_mode_light.png"),
  caption: [Buriza UI - Mobile: Receive assets advanced mode]
)

=== View Balance and Assets

#v(2em)

#figure(
  image("images/screenshots/mobile/view_balance_and_assets_dark.png", width:50%),
  caption: [Buriza UI - Mobile: View balance and assets]
)

#figure(
  image("images/screenshots/mobile/view_balance_and_assets_light.png"),
  caption: [Buriza UI - Mobile: View balance and assets]
)

#figure(
  image("images/screenshots/mobile/header_animation_dark.png"),
  caption: [Buriza UI - Mobile: Header animation]
)

#figure(
  image("images/screenshots/mobile/header_animation_light.png"),
  caption: [Buriza UI - Mobile: Header animation]
)

=== Transaction History

#v(2em)

#figure(
  image("images/screenshots/mobile/transaction_history_dark.jpeg", width: 50%),
  caption: [Buriza UI - Mobile: Transaction history]
)

#figure(
  image("images/screenshots/mobile/transaction_history_light.png"),
  caption: [Buriza UI - Mobile: Transaction history]
)

#figure(
  image("images/screenshots/mobile/transaction_summary_dark.png"),
  caption: [Buriza UI - Mobile: Transaction summary]
)

#figure(
  image("images/screenshots/mobile/transaction_summary_light.png"),
  caption: [Buriza UI - Mobile: Transaction summary]
)

=== Full Node Setup

#v(2em)

#figure(
  image("images/screenshots/mobile/settings_dark.png", width:50%),
  caption: [Buriza UI - Mobile: Settings]
)

#figure(
  image("images/screenshots/mobile/settings_light.png"),
  caption: [Buriza UI - Mobile: Settings]
)

#figure(
  image("images/screenshots/mobile/node_set_up_dark.png"),
  caption: [Buriza UI - Mobile: Node setup]
)

#figure(
  image("images/screenshots/mobile/node_set_up_light.png"),
  caption: [Buriza UI - Mobile: Node setup]
)

=== dApp Access

#v(2em)

#figure(
  image("images/screenshots/mobile/dapp_explorer_dark.png", width: 50%),
  caption: [Buriza UI - Mobile: dApp explorer]
)

#figure(
  image("images/screenshots/mobile/dapp_explorer_light.png"),
  caption: [Buriza UI - Mobile: dApp explorer]
)

#figure(
  image("images/screenshots/mobile/dapp_permission_request_dark.png"),
  caption: [Buriza UI - Mobile: dApp explorer authorization request]
)

#figure(
  image("images/screenshots/mobile/dapp_permission_request_light.png"),
  caption: [Buriza UI - Mobile: dApp explorer authorization request]
)

=== Wallet Creation and Account Management

#v(2em)

#figure(
  image("images/screenshots/mobile/manage_wallet_dark.png", width: 50%),
  caption: [Buriza UI - Mobile: Manage wallet]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_light.png"),
  caption: [Buriza UI - Mobile: Manage wallet]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_drop_down_dark.png"),
  caption: [Buriza UI - Mobile: Manage wallet]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_drop_down_light.png"),
  caption: [Buriza UI - Mobile: Manage wallet]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_add_new_account_dark.png"),
  caption: [Buriza UI - Mobile: Manage wallet - add new account]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_add_account_light.png"),
  caption: [Buriza UI - Mobile: Manage wallet - add new account]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_edit_account_dark.png"),
  caption: [Buriza UI - Mobile: Manage wallet - edit account]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_edit_account_light.png"),
  caption: [Buriza UI - Mobile: Manage wallet - edit account]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_add_new_wallet_dark.png"),
  caption: [Buriza UI - Mobile: Manage wallet - add new wallet]
)

#figure(
  image("images/screenshots/mobile/manage_wallet_add_new_wallet_light.png"),
  caption: [Buriza UI - Mobile: Manage wallet - add new wallet]
)

#figure(
  image("images/screenshots/mobile/create_wallet_save_recovery_phrase_dark.png"),
  caption: [Buriza UI - Mobile: Create new wallet - save recovery phrase]
)

#figure(
  image("images/screenshots/mobile/create_wallet_save_recovery_phrrase_light.png"),
  caption: [Buriza UI - Mobile: Create new wallet - save recovery phrase]
)

#figure(
  image("images/screenshots/mobile/create_wallet_verify_recovery_phrase_dark.png"),
  caption: [Buriza UI - Mobile: Create new wallet - verify recovery phrase]
)

#figure(
  image("images/screenshots/mobile/create_wallet_verify_recovery_phrase_light.png"),
  caption: [Buriza UI - Mobile: Create new wallet - verify recovery phrase]
)

#figure(
  image("images/screenshots/mobile/create_wallet_set_up_dark.png"),
  caption: [Buriza UI - Mobile: Create new wallet - setup]
)

#figure(
  image("images/screenshots/mobile/create_wallet_setup_light.png"),
  caption: [Buriza UI - Mobile: Create new wallet - setup]
)

#figure(
  image("images/screenshots/mobile/create_wallet_authentication_screen_dark.png"),
  caption: [Buriza UI - Mobile: Create new wallet - authentication]
)

#figure(
  image("images/screenshots/mobile/create_wallet_authentication_screen_light.png"),
  caption: [Buriza UI - Mobile: Create new wallet - authentication]
)

#figure(
  image("images/screenshots/mobile/create_wallet_success_screen_dark.png"),
  caption: [Buriza UI - Mobile: Create new wallet - success screen]
)

#figure(
  image("images/screenshots/mobile/create_wallet_success_screen_light.png"),
  caption: [Buriza UI - Mobile: Create new wallet - success screen]
)

#pagebreak()

== Browser

=== Send Assets

#figure(
  image("images/screenshots/browser/send_assets_dark.png"),
  caption: [Buriza UI - Browser: Send assets]
)

#figure(
  image("images/screenshots/browser/send_assets_light.png"),
  caption: [Buriza UI - Browser: Send assets]
)

#figure(
  image("images/screenshots/browser/send_assets_add_recipient_dark.png"),
  caption: [Buriza UI - Browser: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/browser/send_assets_add_recipient_light.png"),
  caption: [Buriza UI - Browser: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/browser/send_assets_add_token_dark.png"),
  caption: [Buriza UI - Browser: Send assets with an additional token]
)

#figure(
  image("images/screenshots/browser/send_assets_add_token_light.png"),
  caption: [Buriza UI - Browser: Send assets with an additional token]
)

#figure(
  image("images/screenshots/browser/send_assets_transaction_summary_dark.png"),
  caption: [Buriza UI - Browser: Send assets transaction summary]
)

#figure(
  image("images/screenshots/browser/send_assets_transaction_summary_light.png"),
  caption: [Buriza UI - Browser: Send assets transaction summary]
)

#figure(
  image("images/screenshots/browser/send_assets_success_screen_dark.png"),
  caption: [Buriza UI - Browser: Send assets success screen]
)

#figure(
  image("images/screenshots/browser/send_assets_success_screen_light.png"),
  caption: [Buriza UI - Browser: Send assets success screen]
)

=== Receiving Assets

#figure(
  image("images/screenshots/browser/receive_assets_dark.png"),
  caption: [Buriza UI - Browser: Receive assets]
)

#figure(
  image("images/screenshots/browser/receive_assets_light.png"),
  caption: [Buriza UI - Browser: Receive assets]
)

#figure(
  image("images/screenshots/browser/receive_assets_advanced_mode_dark.png"),
  caption: [Buriza UI - Browser: Receive assets advanced mode]
)

#figure(
  image("images/screenshots/browser/receive_assets_advanced_mode_light.png"),
  caption: [Buriza UI - Browser: Receive assets advanced mode]
)

=== View Balance and Assets

#figure(
  image("images/screenshots/browser/view_balance_and_assets_dark.png"),
  caption: [Buriza UI - Browser: View balance and assets]
)

#figure(
  image("images/screenshots/browser/view_balance_and_assets_light.png"),
  caption: [Buriza UI - Browser: View balance and assets]
)

#figure(
  image("images/screenshots/browser/header_animation_dark.png"),
  caption: [Buriza UI - Browser: Header animation]
)

#figure(
  image("images/screenshots/browser/header_animation_light.png"),
  caption: [Buriza UI - Browser: Header animation]
)

=== Asset Selection

#figure(
  image("images/screenshots/browser/asset_selection_dark.png"),
  caption: [Buriza UI - Browser: Asset selection]
)

#figure(
  image("images/screenshots/browser/asset_selection_light.png"),
  caption: [Buriza UI - Browser: Asset selection]
)

=== Transaction History

#figure(
  image("images/screenshots/browser/transaction_history_dark.png"),
  caption: [Buriza UI - Browser: Transaction history]
)

#figure(
  image("images/screenshots/browser/transaction_history_light.png"),
  caption: [Buriza UI - Browser: Transaction history]
)

#figure(
  image("images/screenshots/browser/transaction_summary_dark.png"),
  caption: [Buriza UI - Browser: Transaction summary]
)

#figure(
  image("images/screenshots/browser/transaction_summary_light.png"),
  caption: [Buriza UI - Browser: Transaction summary]
)

=== Full Node Setup

#figure(
  image("images/screenshots/browser/settings_dark.png"),
  caption: [Buriza UI - Browser: Settings]
)

#figure(
  image("images/screenshots/browser/settings_light.png"),
  caption: [Buriza UI - Browser: Settings]
)

#figure(
  image("images/screenshots/browser/node_setup_checked_dark.png"),
  caption: [Buriza UI - Browser: Node setup with full node]
)

#figure(
  image("images/screenshots/browser/node_setup_checked_light.png"),
  caption: [Buriza UI - Browser: Node setup with full node]
)

#figure(
  image("images/screenshots/browser/node_setup_unchecked_dark.png"),
  caption: [Buriza UI - Browser: Node setup]
)

#figure(
  image("images/screenshots/browser/node_setup_unchecked_light.png"),
  caption: [Buriza UI - Browser: Node setup]
)

=== dApp Access

#figure(
  image("images/screenshots/browser/dapp_explorer_dark.png"),
  caption: [Buriza UI - Browser: dApp explorer]
)

#figure(
  image("images/screenshots/browser/dapp_explorer_light.png"),
  caption: [Buriza UI - Browser: dApp explorer]
)

#figure(
  image("images/screenshots/browser/dapp_explorer_permission_request_dark.png"),
  caption: [Buriza UI - Browser: dApp authorization request]
)

#figure(
  image("images/screenshots/browser/dapp_explorer_permission_request_light.png"),
  caption: [Buriza UI - Browser: dApp authorization request]
)


#pagebreak()

== Desktop

=== Send Assets

#figure(
  image("images/screenshots/desktop/send_assets_dark.png"),
  caption: [Buriza UI - Desktop: Send assets]
)

#figure(
  image("images/screenshots/desktop/send_assets_light.png"),
  caption: [Buriza UI - Desktop: Send assets]
)

#figure(
  image("images/screenshots/desktop/send_assets_add_recipient_dark.png"),
  caption: [Buriza UI - Desktop: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/desktop/send_assets_add_recipient_light.png"),
  caption: [Buriza UI - Desktop: Send assets with an additional recipient]
)

#figure(
  image("images/screenshots/desktop/send_assets_add_token_dark.png"),
  caption: [Buriza UI - Desktop: Send assets with an additional token]
)

#figure(
  image("images/screenshots/desktop/send_assets_add_token_light.png"),
  caption: [Buriza UI - Desktop: Send assets with an additional token]
)

#figure(
  image("images/screenshots/desktop/send_assets_transaction_summary_add_recipient_dark.png"),
  caption: [Buriza UI - Desktop: Send assets transaction summary with added recipient]
)

#figure(
  image("images/screenshots/desktop/send_assets_transaction_summary_add_recipient_light.png"),
  caption: [Buriza UI - Desktop: Send assets transaction summary with added recipient]
)

#figure(
  image("images/screenshots/desktop/send_assets_transaction_summary_dark.png"),
  caption: [Buriza UI - Desktop: Send assets transaction summary]
)

#figure(
  image("images/screenshots/desktop/send_assets_transaction_summary_light.png"),
  caption: [Buriza UI - Desktop: Send assets transaction summary]
)

#figure(
  image("images/screenshots/desktop/send_assets_success_screen_dark.png"),
  caption: [Buriza UI - Desktop: Send assets success screen]
)

#figure(
  image("images/screenshots/desktop/send_assets_success_screen_light.png"),
  caption: [Buriza UI - Desktop: Send assets success screen]
)

=== Receiving Assets

#figure(
  image("images/screenshots/desktop/receive_assets_dark.png"),
  caption: [Buriza UI - Desktop: Receive assets]
)

#figure(
  image("images/screenshots/desktop/receive_assets_light.png"),
  caption: [Buriza UI - Desktop: Receive assets]
)

#figure(
  image("images/screenshots/desktop/receive_assets_advanced_mode_dark.png"),
  caption: [Buriza UI - Desktop: Receive assets advanced mode]
)

#figure(
  image("images/screenshots/desktop/receive_assets_advanced_mode_light.png"),
  caption: [Buriza UI - Desktop: Receive assets advanced mode]
)

=== View Balance and Assets

#figure(
  image("images/screenshots/desktop/view_balance_and_assets_dark.png"),
  caption: [Buriza UI - Desktop: View balance and assets]
)

#figure(
  image("images/screenshots/desktop/view_balance_and_assets_light.png"),
  caption: [Buriza UI - Desktop: View balance and assets]
)

=== Transaction History

#figure(
  image("images/screenshots/desktop/transaction_history_nft_dark.png"),
  caption: [Buriza UI - Desktop: Transaction history with NFT]
)

#figure(
  image("images/screenshots/desktop/transaction_history_nft_light.png"),
  caption: [Buriza UI - Desktop: Transaction history with NFT]
)

#figure(
  image("images/screenshots/desktop/transaction_history_token_dark.png"),
  caption: [Buriza UI - Desktop: Transaction history with tokens]
)

#figure(
  image("images/screenshots/desktop/transaction_history_token_light.png"),
  caption: [Buriza UI - Desktop: Transaction history with tokens]
)

#figure(
  image("images/screenshots/desktop/transaction_summary_dark.png"),
  caption: [Buriza UI - Desktop: Transaction summary]
)

#figure(
  image("images/screenshots/desktop/transaction_summary_light.png"),
  caption: [Buriza UI - Desktop: Transaction summary]
)

=== Full Node Setup

#figure(
  image("images/screenshots/desktop/settings_dark.png"),
  caption: [Buriza UI - Desktop: Settings]
)

#figure(
  image("images/screenshots/desktop/settings_light.png"),
  caption: [Buriza UI - Desktop: Settings]
)

#figure(
  image("images/screenshots/desktop/node_setup_unchecked_dark.png"),
  caption: [Buriza UI - Desktop: Node setup]
)

#figure(
  image("images/screenshots/desktop/node_setup_unchecked_light.png"),
  caption: [Buriza UI - Desktop: Node setup]
)

=== dApp Access

#figure(
  image("images/screenshots/desktop/dapp_explorer_dark.png"),
  caption: [Buriza UI - Desktop: dApp explorer]
)

#figure(
  image("images/screenshots/desktop/dapp_explorer_light.png"),
  caption: [Buriza UI - Desktop: dApp explorer]
)

#figure(
  image("images/screenshots/desktop/dapp_explorer_permission_request_dark.png"),
  caption: [Buriza UI - Desktop: dApp authorization request]
)

#figure(
  image("images/screenshots/desktop/dapp_explorer_permission_request_light.png"),
  caption: [Buriza UI - Desktop: dApp authorization request]
)

=== Wallet Creation and Account Management

#figure(
  image("images/screenshots/desktop/manage_wallet_dark.png"),
  caption: [Buriza UI - Desktop: Manage wallet]
)

#figure(
  image("images/screenshots/desktop/manage_wallet_light.png"),
  caption: [Buriza UI - Desktop: Manage wallet]
)

#figure(
  image("images/screenshots/desktop/manage_wallet_add_new_account_dark.png"),
  caption: [Buriza UI - Desktop: Manage wallet - add new account]
)

#figure(
  image("images/screenshots/desktop/manage_wallet_add_new_account_light.png"),
  caption: [Buriza UI - Desktop: Manage wallet - add new account]
)

#figure(
  image("images/screenshots/desktop/edit_wallet_dark.png"),
  caption: [Buriza UI - Desktop: Manage wallet - edit account]
)

#figure(
  image("images/screenshots/desktop/edit_wallet_light.png"),
  caption: [Buriza UI - Desktop: Manage wallet - edit account]
)

#figure(
  image("images/screenshots/desktop/add_new_wallet_dark.png"),
  caption: [Buriza UI - Desktop: Manage wallet - add new wallet]
)

#figure(
  image("images/screenshots/desktop/add_new_wallet_light.png"),
  caption: [Buriza UI - Desktop: Manage wallet - add new wallet]
)

#figure(
  image("images/screenshots/desktop/save_recovery_phrase_dark.png"),
  caption: [Buriza UI - Desktop: Create new wallet - save recovery phrase]
)

#figure(
  image("images/screenshots/desktop/save_recovery_phrase_light.png"),
  caption: [Buriza UI - Desktop: Create new wallet - save recovery phrase]
)

#figure(
  image("images/screenshots/desktop/verify_recovery_phrase_dark.png"),
  caption: [Buriza UI - Desktop: Create new wallet - verify recovery phrase]
)

#figure(
  image("images/screenshots/desktop/verify_recovery_phrase_light.png"),
  caption: [Buriza UI - Desktop: Create new wallet - verify recovery phrase]
)

#figure(
  image("images/screenshots/desktop/wallet_setup_dark.png"),
  caption: [Buriza UI - Desktop: Create new wallet - setup]
)

#figure(
  image("images/screenshots/desktop/wallet_setup_light.png"),
  caption: [Buriza UI - Desktop: Create new wallet - setup]
)

#figure(
  image("images/screenshots/desktop/authentication_screen_dark.png"),
  caption: [Buriza UI - Desktop: Create new wallet - authentication]
)

#figure(
  image("images/screenshots/desktop/authentication_screen_light.png"),
  caption: [Buriza UI - Desktop: Create new wallet - authentication]
)

#figure(
  image("images/screenshots/desktop/success_screen_dark.png"),
  caption: [Buriza UI - Desktop: Create new wallet - success screen]
)

#figure(
  image("images/screenshots/desktop/success_screen_light.png"),
  caption: [Buriza UI - Desktop: Create new wallet - success screen]
)


= Platform Development Setup

This section provides step-by-step instructions for building and running Buriza from source. As an open-source Cardano wallet suite, Buriza supports multiple deployment targets including desktop applications, mobile platforms, browser extensions, and progressive web apps.

== Prerequisites

*System Requirements:*
- #link("https://dotnet.microsoft.com/download/dotnet/9.0")[.NET 9 SDK] (v9.0.0+)
- #link("https://nodejs.org/")[Node.js] (v22.0.0+ LTS)
- #link("https://bun.sh/")[Bun] (v1.2.0+)
- #link("https://git-scm.com/")[Git] (v2.39.0+)

*Platform-Specific Requirements:*
- *iOS Development:* macOS, Xcode, Apple Developer account
- *Android Development:* Android SDK, Android Studio
- *Browser Extension:* Chrome/Chromium or Firefox developer mode

== Installation

Clone the repository and install dependencies:

```bash
git clone git@github.com:SAIB-Inc/Buriza.git
cd Buriza

# Restore workloads for MAUI development
dotnet workload restore

# Install CSS dependencies
cd src/Buriza.UI/wwwroot && bun install

# Build the entire solution
dotnet build
```

== Platform Development Instructions

=== Desktop Application (macOS)

```bash
# Navigate to app directory
cd src/Buriza.App

# Build and run macOS application
dotnet build -f net9.0-maccatalyst && dotnet run -f net9.0-maccatalyst
```

=== iOS Simulator

```bash
# Install required workloads
dotnet workload restore

# Build for iOS simulator
cd src/Buriza.App && dotnet build . -f net9.0-ios

# Install and launch on simulator
xcrun simctl boot "iPhone 16 Pro"  # or any available device
xcrun simctl install booted bin/Debug/net9.0-ios/iossimulator-arm64/Buriza.App.app
xcrun simctl launch booted com.saibinc.buriza
```

#pagebreak()

=== Physical iPhone

*Prerequisites:*
1. Change bundle ID to `com.yourname.buriza` in `Buriza.App.csproj`
2. Create Xcode project with same bundle ID to generate provisioning profile

```bash
# Build for physical device
cd src/Buriza.App && dotnet build . -f net9.0-ios -p:RuntimeIdentifier=ios-arm64

# Get device ID
xcrun devicectl list devices

# Install to connected device
xcrun devicectl device install app --device [device-id] bin/Debug/net9.0-ios/ios-arm64/Buriza.App.app
```

=== Android Development

*Prerequisites:*
1. Install Android Studio or Android SDK
2. Install OpenJDK 11+ and add to system PATH (#link("https://learn.microsoft.com/java/openjdk/download")[Download])

*Building and Running:*

```bash
# Start Android emulator
~/Library/Android/sdk/emulator/emulator -avd <emulator-name> &

# Wait for device to boot
~/Library/Android/sdk/platform-tools/adb wait-for-device

# Build and deploy to emulator/device
cd src/Buriza.App && dotnet build -t:Run -f net9.0-android

# Build for specific device architecture
dotnet build -t:Run -f net9.0-android -p:RuntimeIdentifier=android-arm64
```

*Useful Commands:*
```bash
# List available emulators
~/Library/Android/sdk/emulator/emulator -list-avds

# Check connected devices
~/Library/Android/sdk/platform-tools/adb devices

# Uninstall app
~/Library/Android/sdk/platform-tools/adb uninstall com.saibinc.buriza

# View app logs
~/Library/Android/sdk/platform-tools/adb logcat | grep -i buriza
```

*System Requirements:*
- Android 6.0+ / API 23+
- Minimum 2GB free disk space for emulator

=== Browser Extension

```bash
# Build browser extension
cd src/Buriza.Extension
dotnet build -c Release

# Load unpacked extension in browser:
```
1. Navigate to browser's extension management page
2. Enable "Developer mode"
3. Click "Load unpacked" and select `src/Buriza.Extension/bin/Release/net9.0/browserextension/`

=== Progressive Web App

```bash
# Run web application
cd src/Buriza.Web
dotnet run

# Run CSS watch (in separate terminal)
cd src/Buriza.UI/wwwroot && bun watch
```

#pagebreak()

= Conclusion

This document demonstrates the successful completion of Milestone 2 for the Buriza wallet suite, showcasing significant progress in UI/UX design and front-end development. Through the adoption of Material Design 3 principles and the introduction of Buriza Frostling as the new mascot, the project has evolved into a more refined, accessible, and visually cohesive application that embodies the core values of security, trust, and power.

The technical implementation leveraging .NET 9, Blazor, and a shared component architecture has proven highly effective, enabling a single codebase to serve multiple deployment targets across mobile, desktop, and browser platforms. This approach has dramatically reduced development complexity while ensuring consistent user experiences across all platforms.

However, achieving Milestone 2 represents just the beginning of Buriza's journey. The development team remains committed to continuous improvement and feature expansion. Future developments will focus on enhancing wallet functionality, improving performance, and incorporating community feedback to create an even more robust and user-centered Cardano wallet experience.

SAIB Inc. is dedicated to maintaining and evolving Buriza as a leading open-source, decentralized Cardano wallet solution. The project will continue to uphold its principles of complete decentralization, self-verifiability, and user sovereignty while adapting to the evolving needs of the Cardano ecosystem and its community.

#pagebreak()

= Links

This section contains links relevant to the Buriza project:

- #link("https://github.com/SAIB-Inc/Buriza")[Buriza GitHub repository]

- #link("https://saibph-my.sharepoint.com/:f:/g/personal/accounts_saib_dev/EpK_1IrageVHhtTzjgJrDDUBUkIoIkfHMCJcEodiE7uq-Q?e=ExWrYd")[Screenshots and Videos]

- #link("https://saibph-my.sharepoint.com/:b:/g/personal/accounts_saib_dev/Ea17Rbt3BLlFvIbwLlNMhMcB0LgqVCZc94Jx55p-FHo8zw?e=KheTGY")[Buriza Design Report]

- #link("https://www.figma.com/design/9Bw3Nuh8UF9xYHkURwJeoI/Buriza-with-MD3?node-id=379-125141&t=noBZ3ME2edbCdXht-1")[Buriza Material Design 3]

Project Catalyst:
- #link("https://milestones.projectcatalyst.io/projects/1300168/milestones/1")[Project Catalyst: Buriza.Browser]

- #link("https://milestones.projectcatalyst.io/projects/1300169/milestones/1")[Project Catalyst: Buriza.Desktop]

- #link("https://milestones.projectcatalyst.io/projects/1300170/milestones/1")[Project Catalyst: Buriza.Mobile]

#pagebreak()

= References

Google. (2025). _Material Design 3_. Material Design. https://m3.material.io/

Google. (2023). _Manifest V3 - Chrome Extensions_. Chrome Developers. https://developer.chrome.com/docs/extensions/mv3/

Material Design. (2025). _Material Design 3_. Google. #link("https://m3.material.io/")[https://m3.material.io/]

Microsoft. (2024). _ASP.NET Core Blazor_. Microsoft Learn. https://learn.microsoft.com/en-us/aspnet/core/blazor/

Microsoft. (2024). _.NET Multi-platform App UI (.NET MAUI)_. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/maui/

Microsoft. (2024). _BlazorWebView for .NET MAUI_. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview

Mozilla. (2023). _Progressive web apps (PWAs)_. MDN Web Docs. https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps

MudBlazor Team. (2024). _MudBlazor - Blazor component library_. MudBlazor. https://mudblazor.com/

Tailwind Labs. (2024). _Tailwind CSS - A utility-first CSS framework_. Tailwind CSS. https://tailwindcss.com/

WebAssembly Community Group. (2023). _WebAssembly_. WebAssembly. https://webassembly.org/
