<#
    .SYNOPSIS
        Get-AnalyticsBrowser.

    .DESCRIPTION
        Get-AnalyticsBrowser.

	.PARAMETER Path
		Path to the item that should be published - can work with Language parameter to narrow the publication scope.

	.PARAMETER Id
		Id of the item that should be published - can work with Language parameter to narrow the publication scope.

    .INPUTS
        Sitecore.Data.Items.Item
    
    .OUTPUTS
        Sitecore.Data.Items.Item

    .NOTES
        Help Author: Adam Najmanowicz, Michael West

    .LINK
        https://github.com/SitecorePowerShell/Console/

    .EXAMPLE
        PS master:\> Get-AnalyticsBrowser -Path master:\content\home
#>
