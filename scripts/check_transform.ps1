# Check how CoordinateTransformationService transforms (X1, Y1) vs (X2, Y2)
Select-String -Path "src\InfraBIM.CulvertTool\Services\CoordinateTransformationService.cs" -Pattern "Transform" -Context 5,20
