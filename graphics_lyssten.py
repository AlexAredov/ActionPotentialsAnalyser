import numpy as np
import matplotlib.pyplot as plt

def replace_commas_with_dots(file_name):
    with open(file_name, 'r') as file:
        content = file.read()
    with open(file_name, 'w') as file:
        file.write(content.replace(',', '.'))

file_name = '17.txt'

# Заменяем запятые на точки
replace_commas_with_dots(file_name)

# Считываем данные из файла
data = np.loadtxt(file_name)

# Разделяем данные на координаты X и Y
x_data = data[:, 0]
y_data = data[:, 1]

# Строим график
plt.plot(x_data, y_data, color = 'red', label='Соединенные точки', linestyle='-', marker='', markersize=2, alpha=1)
plt.scatter(x_data, y_data, label='Исходные данные', s=5)
plt.xlabel('Время')
plt.ylabel('Потенциал')
plt.legend()
plt.show()
